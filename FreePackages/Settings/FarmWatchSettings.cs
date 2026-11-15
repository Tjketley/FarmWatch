using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiSteamFarm.Helpers;
using ArchiSteamFarm.Helpers.Json;

namespace FreePackages;

internal sealed class FarmWatchSettings : SerializableFile {
	private static readonly string SettingsPath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.json");
	internal static FarmWatchSettings Current { get; private set; } = new();
	internal static bool Exists => File.Exists(SettingsPath);

	[JsonInclude]
	public bool AutoClaimEnabled { get; set; }

	[JsonInclude]
	public bool ObserveOnly { get; set; } = true;

	[JsonInclude]
	public ConcurrentDictionary<string, bool> BotAutoClaim { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

	[JsonConstructor]
	internal FarmWatchSettings() => FilePath = SettingsPath;

	protected override Task Save() => Save(this);

	internal static async Task Load() {
		if (!File.Exists(SettingsPath)) {
			Current = new FarmWatchSettings();
			return;
		}

		try {
			string json = await File.ReadAllTextAsync(SettingsPath).ConfigureAwait(false);
			FarmWatchSettings? loaded = json.ToJsonObject<FarmWatchSettings>();
			Current = loaded ?? new FarmWatchSettings();
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
}
