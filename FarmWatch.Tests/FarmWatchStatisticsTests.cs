using System;
using System.Collections.Generic;
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
	public void FarmingDurationAggregationSumsClippedSessions() {
		DateTime periodStart = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
		FarmWatchStatisticsStore.FarmingSession[] sessions = {
			new() { StartedAtUtc = periodStart.AddHours(-1), EndedAtUtc = periodStart.AddHours(2) },
			new() { StartedAtUtc = periodStart.AddHours(3), EndedAtUtc = periodStart.AddHours(5) }
		};
		Assert.AreEqual(3 * 60 * 60, FarmWatchStatisticsMath.SumOverlapSeconds(sessions, periodStart, periodStart.AddDays(1)), 0.001);
	}

	[TestMethod]
	public void ProductInfoBatchingKeepsEveryIdWithinSteamLimit() {
		HashSet<uint> appIds = new();
		for (uint id = 1; id <= 256; id++) appIds.Add(id);
		List<HashSet<uint>?> batches = new();
		foreach ((HashSet<uint>? apps, HashSet<uint>? packages) in ProductInfo.GetProductIDBatches(appIds, null)) {
			Assert.IsNull(packages);
			batches.Add(apps);
		}
		Assert.AreEqual(2, batches.Count);
		Assert.AreEqual(ProductInfo.ItemsPerProductInfoRequest, batches[0]!.Count);
		Assert.AreEqual(1, batches[1]!.Count);
		HashSet<uint> returned = new();
		foreach (HashSet<uint>? batch in batches) foreach (uint id in batch!) returned.Add(id);
		Assert.AreEqual(appIds.Count, returned.Count);
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
