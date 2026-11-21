using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class FarmWatchConnectSerializationTests {
	[TestMethod]
	public void OutboxEnvelopeRoundTripsThroughGeneratedMetadata() {
		ConnectUploader.UploadEnvelope envelope = new() {
			SchemaVersion = 1,
			UploadId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
			InstanceId = "farmwatch-test-instance",
			RecordedAt = new DateTime(2026, 11, 21, 10, 30, 0, DateTimeKind.Utc),
			Statistics = new ConnectUploader.ConnectStatisticsSnapshot {
				FarmWatchVersion = "2.0.0.11",
				ClaimSummary = new FarmWatchStatisticsStore.ClaimSummary { Total = 3, FreeToKeepPackages = 2, FreeDlcApps = 1, Today = 1, ThisMonth = 3 },
				FarmingSummary = new FarmWatchStatisticsStore.FarmingSummary { TotalSessions = 4, ActiveSessions = 1, LifetimeSeconds = 1200, TodaySeconds = 300, ThisMonthSeconds = 1200, UniqueGames = 8 },
				TradeSummary = new FarmWatchStatisticsStore.TradeSummary { CompletedTrades = 5, ManualBaseline = 2, MonitoringTrades = 3, ItemsSent = 7, ItemsReceived = 9, Today = 1, ThisMonth = 3, HistorySource = "live_callbacks_only" },
				BotsTotal = 2,
				BotsConnected = 1,
				LastPicsChangeNumber = 12345
			},
			Claims = new List<ConnectUploader.ConnectClaimSnapshot> {
				new() { ClaimId = "claim-1", TargetType = "Sub", TargetId = 123, ClaimedAt = "2026-11-21T10:00:00.0000000Z", Source = "farmwatch" }
			}
		};

		string json = JsonSerializer.Serialize(envelope, FarmWatchJsonContext.Instance.UploadEnvelope);
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;
		Assert.AreEqual(1, root.GetProperty("schema_version").GetInt32());
		Assert.AreEqual("farmwatch-test-instance", root.GetProperty("instance_id").GetString());
		Assert.AreEqual("2.0.0.11", root.GetProperty("statistics").GetProperty("farmwatch_version").GetString());
		Assert.AreEqual(3, root.GetProperty("statistics").GetProperty("claim_summary").GetProperty("total").GetInt32());
		Assert.AreEqual(123U, root.GetProperty("claims")[0].GetProperty("target_id").GetUInt32());

		List<ConnectUploader.UploadEnvelope>? restored = JsonSerializer.Deserialize("[" + json + "]", FarmWatchJsonContext.Instance.ListUploadEnvelope);
		Assert.IsNotNull(restored);
		Assert.AreEqual(1, restored.Count);
		Assert.AreEqual(envelope.UploadId, restored[0].UploadId);
		Assert.AreEqual(3, restored[0].Statistics.ClaimSummary.Total);
		Assert.AreEqual("claim-1", restored[0].Claims[0].ClaimId);
	}

	[TestMethod]
	public void ReceiverAcknowledgementRequiresLiteralTrue() {
		Assert.IsTrue(ConnectUploader.IsAcknowledged("{\"ok\":true}"));
		Assert.IsFalse(ConnectUploader.IsAcknowledged("{\"ok\":false}"));
		Assert.IsFalse(ConnectUploader.IsAcknowledged("{}"));
		Assert.IsFalse(ConnectUploader.IsAcknowledged("{\"ok\":1}"));
		Assert.IsFalse(ConnectUploader.IsAcknowledged("not-json"));
	}
}
