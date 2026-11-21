using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ArchiSteamFarm.Helpers;
using ArchiSteamFarm.Helpers.Json;

namespace FreePackages;

internal sealed class FarmWatchSettings : SerializableFile {
	// ASF resolves config/ and plugins/ relative to its working directory. Keep
	// FarmWatch's mutable state in a sibling directory so plugin updates cannot
	// replace it and ASF never mistakes it for a bot configuration.
	private static readonly string DataDirectory = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "FarmWatchData"));
	private static readonly string SettingsPath = Path.Combine(DataDirectory, "FarmWatch.json");
	private static readonly string PluginLegacySettingsPath = Path.Combine("plugins", "FarmWatch", "FarmWatch.json");
	private static readonly string ConfigLegacySettingsPath = Path.Combine(ArchiSteamFarm.SharedInfo.ConfigDirectory, "FarmWatch.json");
	internal static FarmWatchSettings Current { get; private set; } = new();
	internal static bool Exists => File.Exists(SettingsPath) || File.Exists(PluginLegacySettingsPath) || File.Exists(ConfigLegacySettingsPath);

	[JsonInclude]
	public bool AutoClaimEnabled { get; set; } = true;
	[JsonInclude]
	public bool ObserveOnly { get; set; }
	[JsonInclude]
	public ConcurrentDictionary<string, bool> BotAutoClaim { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
	[JsonInclude]
	public long ManualTradeBaseline { get; set; }
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

		if (File.Exists(SettingsPath)) {
			Current = await ReadSettings(SettingsPath).ConfigureAwait(false);
			return;
		}

		// Migrate the previous plugin-local location first. The config-directory
		// location is retained as a legacy fallback for installations created by
		// older builds. Never discard a source until the new copy is verified.
		string? migrationSource = File.Exists(PluginLegacySettingsPath)
			? PluginLegacySettingsPath
			: File.Exists(ConfigLegacySettingsPath) ? ConfigLegacySettingsPath : null;

		if (migrationSource == null) {
			Current = new FarmWatchSettings();
			return;
		}

		FarmWatchSettings migrated = await ReadSettings(migrationSource).ConfigureAwait(false);
		await migrated.Save().ConfigureAwait(false);
		_ = await ReadSettings(SettingsPath).ConfigureAwait(false);

		Current = migrated;
		ArchiveMigratedSource(migrationSource);
		ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericInfo("FarmWatch settings migrated to the ASF-root FarmWatchData directory.");
	}

	private static async Task<FarmWatchSettings> ReadSettings(string path) {
		string json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
		return json.ToJsonObject<FarmWatchSettings>() ?? throw new InvalidDataException("FarmWatch settings file did not contain a valid settings object: " + path);
	}

	private static void ArchiveMigratedSource(string sourcePath) {
		try {
			string backupDirectory = Path.Combine(DataDirectory, "Backups");
			Directory.CreateDirectory(backupDirectory);
			string fileName = "FarmWatch.legacy-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json";
			File.Move(sourcePath, Path.Combine(backupDirectory, fileName), false);
		} catch (Exception e) {
			// The verified new copy is already active. Leaving the old source in
			// place is safer than failing startup or risking data loss.
			ArchiSteamFarm.Core.ASF.ArchiLogger.LogGenericWarningException(e);
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
	internal async Task SetManualTradeBaseline(long completedTrades) {
		ManualTradeBaseline = Math.Clamp(completedTrades, 0, 1_000_000_000);
		await Save().ConfigureAwait(false);
	}
	internal Task SaveConnect() => Save();
}
