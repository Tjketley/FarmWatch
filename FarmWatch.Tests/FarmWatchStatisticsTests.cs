using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class FarmWatchStatisticsTests {
	[TestMethod]
	public void DiagnosticsRetainsOnlyNewestLinesInOriginalOrder() {
		string[] lines = { "one", "two", "three", "four", "five" };
		CollectionAssert.AreEqual(new[] { "three", "four", "five" }, FarmWatchDiagnostics.GetLastLines(lines, 3));
	}

	[TestMethod]
	public void DiagnosticsReturnsNoLinesForNonPositiveLimit() {
		CollectionAssert.AreEqual(Array.Empty<string>(), FarmWatchDiagnostics.GetLastLines(new[] { "one" }, 0));
	}

	[TestMethod]
	public void FarmingDurationClipsSessionAtStartOfDay() {
		DateTime dayStart = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
		DateTime sessionStart = dayStart.AddHours(-2);
		DateTime sessionEnd = dayStart.AddHours(3);
		Assert.AreEqual(3 * 60 * 60, FarmWatchStatisticsMath.CalculateOverlapSeconds(sessionStart, sessionEnd, dayStart, dayStart.AddDays(1)), 0.001);
	}

	[TestMethod]
	public void FarmingDurationClipsSessionAtEndOfDay() {
		DateTime dayStart = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
		DateTime sessionStart = dayStart.AddHours(22);
		DateTime sessionEnd = dayStart.AddDays(1).AddHours(2);
		Assert.AreEqual(2 * 60 * 60, FarmWatchStatisticsMath.CalculateOverlapSeconds(sessionStart, sessionEnd, dayStart, dayStart.AddDays(1)), 0.001);
	}

	[TestMethod]
	public void ActiveSessionStopsAtCurrentPeriodEnd() {
		DateTime periodStart = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
		DateTime periodEnd = periodStart.AddHours(4);
		Assert.AreEqual(2 * 60 * 60, FarmWatchStatisticsMath.CalculateOverlapSeconds(periodStart.AddHours(2), null, periodStart, periodEnd), 0.001);
	}

	[TestMethod]
	public void SessionOutsidePeriodContributesZeroDuration() {
		DateTime periodStart = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
		Assert.AreEqual(0, FarmWatchStatisticsMath.CalculateOverlapSeconds(periodStart.AddDays(-2), periodStart.AddDays(-1), periodStart, periodStart.AddDays(1)), 0.001);
	}
}
