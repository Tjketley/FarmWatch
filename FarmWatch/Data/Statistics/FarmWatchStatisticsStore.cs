using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Steam.Cards;

namespace FreePackages;

/// <summary>
/// Durable local event ledger for FarmWatch statistics.
/// </summary>
internal static class FarmWatchStatisticsStore {
	private static readonly object Sync = new();
	private static readonly string StorePath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.statistics.json");
	private static readonly string TemporaryPath = StorePath + ".tmp";
	private static readonly string FarmingPath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.farming.json");
	private static readonly string FarmingTemporaryPath = FarmingPath + ".tmp";
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
	private static List<ClaimEvent> Claims = new();
	private static List<FarmingSession> FarmingSessions = new();
	private static bool Initialized;

	internal sealed class ClaimEvent {
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public string Bot { get; init; } = "";
		public string TargetType { get; init; } = "";
		public uint TargetId { get; init; }
		public DateTime ClaimedAtUtc { get; init; }
		public string Source { get; init; } = "farmwatch";
	}

	internal sealed class FarmingSession {
		public string SessionId { get; init; } = Guid.NewGuid().ToString("N");
		public string Bot { get; init; } = "";
		public DateTime StartedAtUtc { get; init; }
		public DateTime? EndedAtUtc { get; set; }
		public List<uint> AppIds { get; init; } = new();
	}

	internal static void Initialize() {
		lock (Sync) {
			if (Initialized) return;
			Initialized = true;
			try {
				if (File.Exists(StorePath)) {
					List<ClaimEvent>? loaded = JsonSerializer.Deserialize<List<ClaimEvent>>(File.ReadAllText(StorePath), JsonOptions);
					if (loaded != null) Claims = loaded.Where(item => item != null && item.TargetId > 0 && item.ClaimedAtUtc != default).ToList();
				}
				if (File.Exists(FarmingPath)) {
					List<FarmingSession>? sessions = JsonSerializer.Deserialize<List<FarmingSession>>(File.ReadAllText(FarmingPath), JsonOptions);
					if (sessions != null) FarmingSessions = sessions.Where(item => item != null && item.StartedAtUtc != default).ToList();
				}
			} catch (Exception e) {
				ASF.ArchiLogger.LogGenericWarningException(e);
			}
		}
	}

	internal static void RecordSuccessfulClaim(string bot, EPackageType type, uint targetId, DateTime claimedAtUtc) {
		if (targetId == 0 || (type != EPackageType.App && type != EPackageType.Sub)) return;
		Initialize();
		lock (Sync) {
			if (Claims.Any(item => item.Bot.Equals(bot, StringComparison.OrdinalIgnoreCase) && item.TargetType == type.ToString() && item.TargetId == targetId)) return;
			Claims.Add(new ClaimEvent { EventId = Guid.NewGuid().ToString("N"), Bot = bot, TargetType = type.ToString(), TargetId = targetId, ClaimedAtUtc = DateTime.SpecifyKind(claimedAtUtc, DateTimeKind.Utc) });
			SaveClaimsLocked();
		}
	}

	internal static void RecordFarmingStarted(string bot, IReadOnlyCollection<Game> games) {
		ArgumentException.ThrowIfNullOrEmpty(bot);
		Initialize();
		lock (Sync) {
			if (FarmingSessions.Any(item => item.Bot.Equals(bot, StringComparison.OrdinalIgnoreCase) && !item.EndedAtUtc.HasValue)) return;
			FarmingSessions.Add(new FarmingSession {
				SessionId = Guid.NewGuid().ToString("N"),
				Bot = bot,
				StartedAtUtc = DateTime.UtcNow,
				AppIds = games.Where(game => game.AppID > 0).Select(game => game.AppID).Distinct().ToList()
			});
			SaveFarmingLocked();
		}
	}

	internal static void RecordFarmingStopped(string bot) {
		ArgumentException.ThrowIfNullOrEmpty(bot);
		Initialize();
		lock (Sync) {
			FarmingSession? active = FarmingSessions.LastOrDefault(item => item.Bot.Equals(bot, StringComparison.OrdinalIgnoreCase) && !item.EndedAtUtc.HasValue);
			if (active == null) return;
			active.EndedAtUtc = DateTime.UtcNow;
			SaveFarmingLocked();
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

	internal static object GetFarmingSummary() {
		Initialize();
		lock (Sync) {
			DateTime now = DateTime.UtcNow;
			DateTime today = now.Date;
			DateTime month = new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			double SecondsFor(Func<FarmingSession, bool> predicate) => FarmingSessions.Where(predicate).Sum(item => Math.Max(0, ((item.EndedAtUtc ?? now) - item.StartedAtUtc).TotalSeconds));
			return new {
				totalSessions = FarmingSessions.Count,
				activeSessions = FarmingSessions.Count(item => !item.EndedAtUtc.HasValue),
				lifetimeSeconds = SecondsFor(_ => true),
				todaySeconds = SecondsFor(item => item.StartedAtUtc >= today || (item.EndedAtUtc ?? now) >= today),
				thisMonthSeconds = SecondsFor(item => item.StartedAtUtc >= month || (item.EndedAtUtc ?? now) >= month),
				uniqueGames = FarmingSessions.SelectMany(item => item.AppIds).Distinct().Count()
			};
		}
	}

	private static void SaveClaimsLocked() {
		try {
			Directory.CreateDirectory(ArchiSteamFarm.SharedInfo.ConfigDirectory);
			File.WriteAllText(TemporaryPath, JsonSerializer.Serialize(Claims, JsonOptions));
			File.Move(TemporaryPath, StorePath, true);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
		}
	}

	private static void SaveFarmingLocked() {
		try {
			Directory.CreateDirectory(ArchiSteamFarm.SharedInfo.ConfigDirectory);
			File.WriteAllText(FarmingTemporaryPath, JsonSerializer.Serialize(FarmingSessions, JsonOptions));
			File.Move(FarmingTemporaryPath, FarmingPath, true);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
		}
	}
}