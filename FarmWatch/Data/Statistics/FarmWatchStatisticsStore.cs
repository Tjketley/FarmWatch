using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArchiSteamFarm.Core;

namespace FreePackages;

/// <summary>
/// Durable local event ledger for FarmWatch statistics. This is the source of truth;
/// aggregate counters and short-lived rate-limit caches must never be used as history.
/// </summary>
internal static class FarmWatchStatisticsStore {
	private static readonly object Sync = new();
	private static readonly string StorePath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.statistics.json");
	private static readonly string TemporaryPath = StorePath + ".tmp";
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
	private static List<ClaimEvent> Claims = new();
	private static bool Initialized;

	internal sealed class ClaimEvent {
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public string Bot { get; init; } = "";
		public string TargetType { get; init; } = "";
		public uint TargetId { get; init; }
		public DateTime ClaimedAtUtc { get; init; }
		public string Source { get; init; } = "farmwatch";
	}

	internal static void Initialize() {
		lock (Sync) {
			if (Initialized) return;
			Initialized = true;
			try {
				if (!File.Exists(StorePath)) return;
				string json = File.ReadAllText(StorePath);
				List<ClaimEvent>? loaded = JsonSerializer.Deserialize<List<ClaimEvent>>(json, JsonOptions);
				if (loaded != null) Claims = loaded.Where(item => item != null && item.TargetId > 0 && item.ClaimedAtUtc != default).ToList();
			} catch (Exception e) {
				ASF.ArchiLogger.LogGenericWarningException(e);
			}
		}
	}

	internal static void RecordSuccessfulClaim(string bot, EPackageType type, uint targetId, DateTime claimedAtUtc) {
		if (targetId == 0 || (type != EPackageType.App && type != EPackageType.Sub)) return;
		Initialize();
		lock (Sync) {
			// Steam can replay callbacks and a queue retry can revisit a completed target.
			// One FarmWatch claim event per bot + target prevents double counting.
			if (Claims.Any(item => item.Bot.Equals(bot, StringComparison.OrdinalIgnoreCase) && item.TargetType == type.ToString() && item.TargetId == targetId)) return;

			Claims.Add(new ClaimEvent {
				EventId = Guid.NewGuid().ToString("N"),
				Bot = bot,
				TargetType = type.ToString(),
				TargetId = targetId,
				ClaimedAtUtc = DateTime.SpecifyKind(claimedAtUtc, DateTimeKind.Utc)
			});
			SaveLocked();
		}
	}

	internal static ClaimEvent[] GetClaims() {
		Initialize();
		lock (Sync) return Claims.OrderBy(item => item.ClaimedAtUtc).ToArray();
	}

	internal static object GetClaimSummary() {
		ClaimEvent[] claims = GetClaims();
		DateTime today = DateTime.UtcNow.Date;
		DateTime month = new(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		return new {
			total = claims.Length,
			freeToKeepPackages = claims.Count(item => item.TargetType == EPackageType.Sub.ToString()),
			freeDlcApps = claims.Count(item => item.TargetType == EPackageType.App.ToString()),
			today = claims.Count(item => item.ClaimedAtUtc >= today),
			thisMonth = claims.Count(item => item.ClaimedAtUtc >= month)
		};
	}

	private static void SaveLocked() {
		try {
			Directory.CreateDirectory(ArchiSteamFarm.SharedInfo.ConfigDirectory);
			File.WriteAllText(TemporaryPath, JsonSerializer.Serialize(Claims, JsonOptions));
			File.Move(TemporaryPath, StorePath, true);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
		}
	}
}
