using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiSteamFarm.Helpers;
using ArchiSteamFarm.Helpers.Json;

namespace FreePackages;

internal sealed class FarmWatchSettings : SerializableFile {
	private static readonly string DataDirectory = Path.Combine("plugins", "FarmWatch");
	private static readonly string SettingsPath = Path.Combine(DataDirectory, "FarmWatch.json");
	private static readonly string LegacySettingsPath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.json");
	internal static FarmWatchSettings Current { get; private set; } = new();
	internal static bool Exists => File.Exists(SettingsPath) || File.Exists(LegacySettingsPath);

	[JsonInclude]
	public bool AutoClaimEnabled { get; set; } = true;
	[JsonInclude]
	public bool ObserveOnly { get; set; }
	[JsonInclude]
	public ConcurrentDictionary<string, bool> BotAutoClaim { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
	[JsonInclude]
	public bool ConnectEnabled { get; set; }
	[JsonInclude]
	public string ConnectEndpoint { get; set; } = "";
	[JsonInclude]
	public string ConnectToken { get; set; } = "";
	[JsonInclude]
	public string ConnectInstanceId { get; set; } = "";

	[JsonConstructor]
	internal FarmWatchSettings() => FilePath = SettingsPath;
	protected override Task Save() => Save(this);

	internal static async Task Load() {
		Directory.CreateDirectory(DataDirectory);
		try {
			if (File.Exists(SettingsPath)) {
				string json = await File.ReadAllTextAsync(SettingsPath).ConfigureAwait(false);
				Current = json.ToJsonObject<FarmWatchSettings>() ?? new FarmWatchSettings();
				if (File.Exists(LegacySettingsPath)) File.Delete(LegacySettingsPath);
				return;
			}
			if (File.Exists(LegacySettingsPath)) {
				string legacyJson = await File.ReadAllTextAsync(LegacySettingsPath).ConfigureAwait(false);
				Current = legacyJson.ToJsonObject<FarmWatchSettings>() ?? new FarmWatchSettings();
				await Current.Save().ConfigureAwait(false);
				File.Delete(LegacySettingsPath);
				ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericInfo("FarmWatch settings migrated out of the ASF bot configuration directory.");
				return;
			}
			Current = new FarmWatchSettings();
		} catch (Exception e) {
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericWarningException(e);
			Current = new FarmWatchSettings();
		}
	}

	internal async Task SetGlobal(bool? autoClaimEnabled, bool? observeOnly) {
		if (autoClaimEnabled.HasValue) AutoClaimEnabled = autoClaimEnabled.Value;
		if (observeOnly.HasValue) ObserveOnly = observeOnly.Value;
		await Save().ConfigureAwait(false);
	}
	internal async Task SetBot(string botName, bool enabled) {
		BotAutoClaim[botName] = enabled;
		await Save().ConfigureAwait(false);
	}
	internal Task SaveConnect() => Save();
}
