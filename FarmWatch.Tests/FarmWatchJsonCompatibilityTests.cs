using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class FarmWatchJsonCompatibilityTests {
	[TestMethod]
	public void TypeBasedSerializerUsesGeneratedMetadataAndPreservesCamelCase() {
		FarmWatchEvent source = new() {
			Id = 42,
			Level = "info",
			Category = "runtime",
			Message = "compatibility-test"
		};

		string json = FarmWatchJson.Serialize(source);
		using JsonDocument document = JsonDocument.Parse(json);
		Assert.AreEqual(42, document.RootElement.GetProperty("id").GetInt64());
		Assert.AreEqual("compatibility-test", document.RootElement.GetProperty("message").GetString());

		FarmWatchEvent? restored = FarmWatchJson.Deserialize<FarmWatchEvent>(json);
		Assert.IsNotNull(restored);
		Assert.AreEqual(source.Id, restored.Id);
		Assert.AreEqual(source.Message, restored.Message);
	}
}
