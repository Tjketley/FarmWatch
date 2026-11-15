using System;

namespace FreePackages;

internal static class FarmWatchStatisticsMath {
	internal static double CalculateOverlapSeconds(DateTime startedAtUtc, DateTime? endedAtUtc, DateTime periodStartUtc, DateTime periodEndUtc) {
		DateTime sessionEnd = endedAtUtc ?? periodEndUtc;
		DateTime start = startedAtUtc > periodStartUtc ? startedAtUtc : periodStartUtc;
		DateTime end = sessionEnd < periodEndUtc ? sessionEnd : periodEndUtc;
		return Math.Max(0, (end - start).TotalSeconds);
	}
}
