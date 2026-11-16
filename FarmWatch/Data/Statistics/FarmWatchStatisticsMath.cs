using System;
using System.Collections.Generic;

namespace FreePackages;

internal static class FarmWatchStatisticsMath {
	internal static double SumOverlapSeconds(IEnumerable<FarmWatchStatisticsStore.FarmingSession> sessions, DateTime periodStartUtc, DateTime periodEndUtc) {
		ArgumentNullException.ThrowIfNull(sessions);
		double total = 0;
		foreach (FarmWatchStatisticsStore.FarmingSession session in sessions) {
			total += CalculateOverlapSeconds(session.StartedAtUtc, session.EndedAtUtc, periodStartUtc, periodEndUtc);
		}
		return total;
	}

	internal static double CalculateOverlapSeconds(DateTime startedAtUtc, DateTime? endedAtUtc, DateTime periodStartUtc, DateTime periodEndUtc) {
		DateTime sessionEnd = endedAtUtc ?? periodEndUtc;
		DateTime start = startedAtUtc > periodStartUtc ? startedAtUtc : periodStartUtc;
		DateTime end = sessionEnd < periodEndUtc ? sessionEnd : periodEndUtc;
		return Math.Max(0, (end - start).TotalSeconds);
	}
}
