using System.Text.Json;

namespace FreePackages;

/// <summary>
/// Uses generic System.Text.Json APIs backed by the source-generated resolver.
/// The Type-based Serialize(Object, Type, options) overload is not available
/// in the System.Text.Json runtime ASF 6.3.11.7 loads on supported deployments.
/// </summary>
internal static class FarmWatchJson {
	private static JsonSerializerOptions Options => FarmWatchJsonContext.Instance.Options;

	internal static string Serialize<T>(T value) =>
		JsonSerializer.Serialize<T>(value, Options);

	internal static T? Deserialize<T>(string json) =>
		JsonSerializer.Deserialize<T>(json, Options);

	internal static T? Deserialize<T>(ref Utf8JsonReader reader) =>
		JsonSerializer.Deserialize<T>(ref reader, Options);
}
