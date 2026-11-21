using System.Text.Json;
using ArchiSteamFarm.IPC.Responses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FreePackages.Tests;

[TestClass]
public sealed class FarmWatchApiResponseTests {
	[TestMethod]
	public void SuccessfulApiPayloadUsesTheAsfResponseEnvelope() {
		GenericResponse<object> response = new(new { name = "FarmWatch", version = "2.0.0.16" });
		string json = JsonSerializer.Serialize(response);
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;

		Assert.IsTrue(root.GetProperty("Success").GetBoolean());
		Assert.AreEqual("OK", root.GetProperty("Message").GetString());
		Assert.AreEqual("FarmWatch", root.GetProperty("Result").GetProperty("name").GetString());
		Assert.AreEqual("2.0.0.16", root.GetProperty("Result").GetProperty("version").GetString());
	}

	[TestMethod]
	public void FailedApiPayloadUsesTheAsfErrorEnvelope() {
		const string message = "No settings were supplied.";
		GenericResponse response = new(false, message);
		string json = JsonSerializer.Serialize(response);
		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;

		Assert.IsFalse(root.GetProperty("Success").GetBoolean());
		Assert.AreEqual(message, root.GetProperty("Message").GetString());
	}
}
