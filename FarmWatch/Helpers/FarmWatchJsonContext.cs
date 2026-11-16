using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FreePackages;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FarmWatchEvent))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.ClaimEvent))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.TradeEvent))]
[JsonSerializable(typeof(FarmWatchStatisticsStore.FarmingSession))]
[JsonSerializable(typeof(List<FarmWatchStatisticsStore.ClaimEvent>))]
[JsonSerializable(typeof(List<FarmWatchStatisticsStore.TradeEvent>))]
[JsonSerializable(typeof(List<FarmWatchStatisticsStore.FarmingSession>))]
internal partial class FarmWatchJsonContext : JsonSerializerContext;
