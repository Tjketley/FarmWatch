using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreePackages;

[JsonSerializable(typeof(FarmWatchEvent))]
[JsonSerializable(typeof(ConnectUploader.UploadEnvelope))]
[JsonSerializable(typeof(List<ConnectUploader.UploadEnvelope>))]
[JsonSerializable(typeof(ConnectUploader.ConnectStatisticsSnapshot))]
[JsonSerializable(typeof(List<ConnectUploader.ConnectClaimSnapshot>))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.ClaimSummary))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.FarmingSummary))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.TradeSummary))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.ClaimEvent))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.TradeEvent))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.FarmingSession))]
[JsonSerializable(typeof(List<FarmWatchStatisticsStore.ClaimEvent>))]
[JsonSerializable(typeof(List<FarmWatchStatisticsStore.TradeEvent>))]
[JsonSerializable(typeof(List<FarmWatchStatisticsStore.FarmingSession>))]
internal partial class FarmWatchJsonContext : JsonSerializerContext {
	// Keep the source-generated metadata, but supply options at runtime instead
	// of encoding option names in JsonSourceGenerationOptionsAttribute metadata.
	// ASF's trimmed runtime cannot reliably resolve that attribute's named
	// properties, while the generated type metadata remains reflection-free.
	internal static FarmWatchJsonContext Instance { get; } = new(new JsonSerializerOptions {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = false
	});
}
