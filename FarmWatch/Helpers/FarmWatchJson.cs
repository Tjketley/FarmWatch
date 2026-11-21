using System;
using System.Text.Json;

namespace FreePackages;

/// <summary>
/// Uses source-generated metadata through the long-standing Type-based
/// System.Text.Json APIs, avoiding newer JsonTypeInfo overloads that may not
/// exist in every ASF 6.3.11.7 runtime deployment.
/// </summary>
internal static class FarmWatchJson {
	private static JsonSerializerOptions Options => FarmWatchJsonContext.Instance.Options;

	internal static string Serialize<T>(T value) =>
		JsonSerializer.Serialize(value, typeof(T), Options);

	internal static T? Deserialize<T>(string json) =>
		(T?) JsonSerializer.Deserialize(json, typeof(T), Options);

	internal static T? Deserialize<T>(ref Utf8JsonReader reader) =>
		(T?) JsonSerializer.Deserialize(ref reader, typeof(T), Options);
}
