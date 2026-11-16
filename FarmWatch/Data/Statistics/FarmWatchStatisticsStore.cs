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
	private static readonly string TradesPath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.trades.json");
	private static readonly string TradesTemporaryPath = TradesPath + ".tmp";
	private static readonly string FarmingTemporaryPath = FarmingPath + ".tmp";
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
	private static List<ClaimEvent> Claims = new();
	private static List<FarmingSession> FarmingSessions = new();
	private static List<TradeEvent> Trades = new();
	private static bool Initialized;

	internal sealed class ClaimEvent {
		public string EventId { get; init; } = Guid.NewGuid().ToString("N");
		public string Bot { get; init; } = "";
		public string TargetType { get; init; } = "";
		public uint TargetId { get; init; }
		public DateTime ClaimedAtUtc { get; init; }
		public string Source { get; init; } = "farmwatch";
	}

	internal sealed class TradeEvent {
		public string TradeId { get; init; } = "";
		public string Bot { get; init; } = "";
		public DateTime RecordedAtUtc { get; init; }
		public string Source { get; init; } = "live_callback";
		public long ItemsSent { get; init; }
		public long ItemsReceived { get; init; }
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
					List<ClaimEvent>? loaded = JsonSerializer.Deserialize(File.ReadAllText(StorePath), FarmWatchJsonContext.Default.ListClaimEvent);
					if (loaded != null) Claims = loaded.Where(item => item != null && item.TargetId > 0 && item.ClaimedAtUtc != default).ToList();
				}
				if (File.Exists(TradesPath)) {
					List<TradeEvent>? trades = JsonSerializer.Deserialize(File.ReadAllText(TradesPath), FarmWatchJsonContext.Default.ListTradeEvent);
					if (trades != null) Trades = trades.Where(item => item != null && !string.IsNullOrEmpty(item.TradeId)).ToList();
				}
				if (File.Exists(FarmingPath)) {
					List<FarmingSession>? sessions = JsonSerializer.Deserialize(File.ReadAllText(FarmingPath), FarmWatchJsonContext.Default.ListFarmingSession);
					if (sessions != null) FarmingSessions = sessions.Where(item => item != null && item.StartedAtUtc != default).ToList();
				}

				// An active session from the previous process cannot be assumed to have
				// continued farming while ASF was offline. Close it at startup so downtime
				// is never counted as farming time.
				DateTime recoveredAtUtc = DateTime.UtcNow;
				bool recoveredSessions = false;
				foreach (FarmingSession session in FarmingSessions.Where(item => !item.EndedAtUtc.HasValue)) {
					session.EndedAtUtc = recoveredAtUtc > session.StartedAtUtc ? recoveredAtUtc : session.StartedAtUtc;
					recoveredSessions = true;
				}
				if (recoveredSessions) SaveFarmingLocked();
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
			FarmingSession? active = null;
            for (int i = FarmingSessions.Count - 1; i >= 0; i--) {
                FarmingSession candidate = FarmingSessions[i];
                if (candidate.Bot.Equals(bot, StringComparison.OrdinalIgnoreCase) && !candidate.EndedAtUtc.HasValue) {
                    active = candidate;
                    break;
                }
            }
			if (active == null) return;
			active.EndedAtUtc = DateTime.UtcNow;
			SaveFarmingLocked();
		}
	}


	internal static void RecordAcceptedTrade(string bot, ulong tradeOfferId, long itemsSent, long itemsReceived) {
		if (string.IsNullOrEmpty(bot) || tradeOfferId == 0) return;
		Initialize();
		lock (Sync) {
			string tradeId = tradeOfferId.ToString(System.Globalization.CultureInfo.InvariantCulture);
			if (Trades.Any(item => item.TradeId == tradeId && item.Bot.Equals(bot, StringComparison.OrdinalIgnoreCase))) return;
			Trades.Add(new TradeEvent { TradeId = tradeId, Bot = bot, RecordedAtUtc = DateTime.UtcNow, ItemsSent = itemsSent, ItemsReceived = itemsReceived });
			SaveTradesLocked();
		}
	}

	internal static object GetTradeSummary() {
		Initialize();
		lock (Sync) {
			DateTime now = DateTime.UtcNow;
			DateTime today = now.Date;
			DateTime month = new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			return new {
				completedTrades = Trades.Count,
				itemsSent = SumTradeItems(sent: true),
				itemsReceived = SumTradeItems(sent: false),
				today = Trades.Count(item => item.RecordedAtUtc >= today),
				thisMonth = Trades.Count(item => item.RecordedAtUtc >= month),
				historySource = "live_callbacks_only"
			};
		}
	}

	private static void SaveTradesLocked() {
		try {
			Directory.CreateDirectory(ArchiSteamFarm.SharedInfo.ConfigDirectory);
			File.WriteAllText(TradesTemporaryPath, JsonSerializer.Serialize(Trades, FarmWatchJsonContext.Default.ListTradeEvent));
			File.Move(TradesTemporaryPath, TradesPath, true);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
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

	private static double OverlapSeconds(FarmingSession session, DateTime periodStart, DateTime periodEnd) =>
		FarmWatchStatisticsMath.CalculateOverlapSeconds(session.StartedAtUtc, session.EndedAtUtc, periodStart, periodEnd);

	private static long SumTradeItems(bool sent) {
		long total = 0;
		foreach (TradeEvent trade in Trades) total += sent ? trade.ItemsSent : trade.ItemsReceived;
		return total;
	}

	internal static object GetFarmingSummary() {
		Initialize();
		lock (Sync) {
			DateTime now = DateTime.UtcNow;
			DateTime today = now.Date;
			DateTime month = new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			return new {
				totalSessions = FarmingSessions.Count,
				activeSessions = FarmingSessions.Count(item => !item.EndedAtUtc.HasValue),
				lifetimeSeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, DateTime.MinValue, now),
				todaySeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, today, now),
				thisMonthSeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, month, now),
				uniqueGames = FarmingSessions.SelectMany(item => item.AppIds).Distinct().Count()
			};
		}
	}

	private static ClaimEvent[] GetRecentClaims(int limit) {
		List<ClaimEvent> recent = new(Claims);
		recent.Sort((left, right) => right.ClaimedAtUtc.CompareTo(left.ClaimedAtUtc));
		if (recent.Count > limit) recent.RemoveRange(limit, recent.Count - limit);
		return recent.ToArray();
	}

	private static FarmingSession[] GetRecentFarmingSessions(int limit) {
		List<FarmingSession> recent = new(FarmingSessions);
		recent.Sort((left, right) => right.StartedAtUtc.CompareTo(left.StartedAtUtc));
		if (recent.Count > limit) recent.RemoveRange(limit, recent.Count - limit);
		return recent.ToArray();
	}

	private static TradeEvent[] GetRecentTrades(int limit) {
		List<TradeEvent> recent = new(Trades);
		recent.Sort((left, right) => right.RecordedAtUtc.CompareTo(left.RecordedAtUtc));
		if (recent.Count > limit) recent.RemoveRange(limit, recent.Count - limit);
		return recent.ToArray();
	}

	internal static object GetDashboardStatistics(int historyLimit = 50) {
		Initialize();
		lock (Sync) {
			DateTime now = DateTime.UtcNow;
			DateTime today = now.Date;
			DateTime month = new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime chartStart = today.AddDays(-29);
			var dailyActivity = Enumerable.Range(0, 30).Select(offset => {
				DateTime dayStart = chartStart.AddDays(offset);
				DateTime dayEnd = dayStart.AddDays(1) < now ? dayStart.AddDays(1) : now;
				double farmingSeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, dayStart, dayEnd);
				return new {
					date = dayStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
					claims = Claims.Count(item => item.ClaimedAtUtc >= dayStart && item.ClaimedAtUtc < dayEnd),
					farmingSeconds,
					trades = Trades.Count(item => item.RecordedAtUtc >= dayStart && item.RecordedAtUtc < dayEnd)
				};
			}).ToArray();
			return new {
				generatedAtUtc = now,
				dailyActivity,
				claims = new {
					total = Claims.Count,
					freeToKeepPackages = Claims.Count(item => item.TargetType == EPackageType.Sub.ToString()),
					freeDlcApps = Claims.Count(item => item.TargetType == EPackageType.App.ToString()),
					today = Claims.Count(item => item.ClaimedAtUtc >= today),
					thisMonth = Claims.Count(item => item.ClaimedAtUtc >= month),
					recent = GetRecentClaims(historyLimit)
				},
				farming = new {
					totalSessions = FarmingSessions.Count,
					activeSessions = FarmingSessions.Count(item => !item.EndedAtUtc.HasValue),
					lifetimeSeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, DateTime.MinValue, now),
					todaySeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, today, now),
					thisMonthSeconds = FarmWatchStatisticsMath.SumOverlapSeconds(FarmingSessions, month, now),
					uniqueGames = FarmingSessions.SelectMany(item => item.AppIds).Distinct().Count(),
					recent = GetRecentFarmingSessions(historyLimit)
				},
				trades = new {
					completedTrades = Trades.Count,
					itemsSent = SumTradeItems(sent: true),
					itemsReceived = SumTradeItems(sent: false),
					today = Trades.Count(item => item.RecordedAtUtc >= today),
					thisMonth = Trades.Count(item => item.RecordedAtUtc >= month),
					historySource = "live_callbacks_only",
					recent = GetRecentTrades(historyLimit)
				}
			};
		}
	}

	private static void SaveClaimsLocked() {
		try {
			Directory.CreateDirectory(ArchiSteamFarm.SharedInfo.ConfigDirectory);
			File.WriteAllText(TemporaryPath, JsonSerializer.Serialize(Claims, FarmWatchJsonContext.Default.ListClaimEvent));
			File.Move(TemporaryPath, StorePath, true);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
		}
	}

	private static void SaveFarmingLocked() {
		try {
			Directory.CreateDirectory(ArchiSteamFarm.SharedInfo.ConfigDirectory);
			File.WriteAllText(FarmingTemporaryPath, JsonSerializer.Serialize(FarmingSessions, FarmWatchJsonContext.Default.ListFarmingSession));
			File.Move(FarmingTemporaryPath, FarmingPath, true);
		} catch (Exception e) {
			ASF.ArchiLogger.LogGenericWarningException(e);
		}
	}
}