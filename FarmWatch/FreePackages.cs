using System;
using System.Collections.Generic;
using System.Linq;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Plugins.Interfaces;
using SteamKit2;
using System.Text.Json;
using ArchiSteamFarm.Helpers.Json;
using ArchiSteamFarm.Web.GitHub.Data;

namespace FreePackages {
	[Export(typeof(IPlugin))]
	public sealed class FreePackages : IASF, IBotModules, ISteamPICSChanges, IBotSteamClient, IBotConnection, IBotCommand2, IGitHubPluginUpdates, IBotCardsFarmerInfo, IBotTradeOfferResults {
		public string Name => "FarmWatch";
		public string RepositoryName => "Tjketley/FarmWatch-2";
		public Version Version => typeof(FreePackages).Assembly.GetName().Version ?? new Version("0");

		// Releases contain both the plugin and standalone ASF UI ZIP. Select the
		// versioned plugin asset explicitly so ASF never tries to install the UI.
		public Task<ReleaseAsset?> GetTargetReleaseAsset(Version asfVersion, string asfVariant, Version newPluginVersion, IReadOnlyCollection<ReleaseAsset> releaseAssets, CancellationToken cancellationToken = default) {
			ArgumentNullException.ThrowIfNull(asfVersion);
			ArgumentException.ThrowIfNullOrEmpty(asfVariant);
			ArgumentNullException.ThrowIfNull(newPluginVersion);
			ArgumentNullException.ThrowIfNull(releaseAssets);

			// The current plugin build is pinned to ASF 6.3.11.7. Do not offer an
			// automatic update for an ASF API version that has not been validated.
			if (!asfVersion.Equals(new Version(6, 3, 11, 7))) {
				return Task.FromResult<ReleaseAsset?>(null);
			}

			string expectedAssetName = $"FarmWatch-{newPluginVersion}.zip";
			ReleaseAsset? targetAsset = releaseAssets.FirstOrDefault(asset => asset.Name.Equals(expectedAssetName, StringComparison.OrdinalIgnoreCase));
			return Task.FromResult(targetAsset);
		}
		internal static GlobalCache? GlobalCache;
		private static readonly object GlobalCacheLock = new();
		private static Task<GlobalCache>? GlobalCacheLoadTask;

		internal static async Task InitializeGlobalCache() {
			Task<GlobalCache> loadTask;
			lock (GlobalCacheLock) {
				GlobalCacheLoadTask ??= FreePackages.GlobalCache == null ? global::FreePackages.GlobalCache.CreateOrLoad() : Task.FromResult(FreePackages.GlobalCache!);
				loadTask = GlobalCacheLoadTask;
			}

			GlobalCache = await loadTask.ConfigureAwait(false);
		}
		// Global kill switch. Missing configuration is deliberately disabled.
		internal static bool FarmWatchAutoClaimEnabled { get; private set; } = false;
		// Observe-only is the safe default. Auto-claim requires an explicit opt-out plus both enable switches.
		internal static bool FarmWatchObserveOnly { get; private set; } = true;
		internal static void ApplyGlobalSettings(bool autoClaimEnabled, bool observeOnly) {
			FarmWatchAutoClaimEnabled = autoClaimEnabled;
			FarmWatchObserveOnly = observeOnly;
		}

		public Task OnLoaded() {
			ASF.ArchiLogger.LogGenericInfo("FarmWatch ASF Plugin (based on Citrinate/FreePackages, Apache-2.0)");

			return Task.CompletedTask;
		}

		public async Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
			return await Commands.Response(bot, access, steamID, message, args).ConfigureAwait(false);
		}

		public async Task OnASFInit(IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			bool configuredAutoClaim = additionalConfigProperties != null &&
				additionalConfigProperties.TryGetValue("FarmWatchAutoClaimEnabled", out JsonElement autoClaimSetting) &&
				autoClaimSetting.ValueKind == JsonValueKind.True;
			bool configuredObserveOnly = additionalConfigProperties == null ||
				!additionalConfigProperties.TryGetValue("FarmWatchObserveOnly", out JsonElement observeOnlySetting) ||
				observeOnlySetting.ValueKind != JsonValueKind.False;

			await FarmWatchSettings.Load().ConfigureAwait(false);
			if (!FarmWatchSettings.Exists) {
				FarmWatchSettings.Current.AutoClaimEnabled = configuredAutoClaim;
				FarmWatchSettings.Current.ObserveOnly = configuredObserveOnly;
				await FarmWatchSettings.Current.SetGlobal(configuredAutoClaim, configuredObserveOnly).ConfigureAwait(false);
			}
			FarmWatchAutoClaimEnabled = FarmWatchSettings.Current.AutoClaimEnabled;
			FarmWatchObserveOnly = FarmWatchSettings.Current.ObserveOnly;
			FarmWatchDiagnostics.Initialize();
			FarmWatchStatisticsStore.Initialize();

			ASF.ArchiLogger.LogGenericInfo("FarmWatch auto-claim global switch : " + FarmWatchAutoClaimEnabled.ToString());
			ASF.ArchiLogger.LogGenericInfo("FarmWatch observe-only mode : " + FarmWatchObserveOnly.ToString());
			FarmWatchDiagnostics.Record("runtime", $"FarmWatch initialised. Version {typeof(FreePackages).Assembly.GetName().Version}; autoClaim={FarmWatchAutoClaimEnabled}; observeOnly={FarmWatchObserveOnly}.");

			await InitializeGlobalCache().ConfigureAwait(false);

			await ConnectUploader.Initialize().ConfigureAwait(false);

			CardApps.Update();
			ASFInfo.Update();
		}

		public async Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			if (additionalConfigProperties == null) {
				return;
			}

			bool isEnabled = false;
			uint? packageLimit = null;
			bool pauseWhilePlaying = false;
			bool pauseWhileFarming = false;
			bool farmWatchEnabled = false;
			List<FilterConfig> filterConfigs = new();

			foreach (KeyValuePair<string, JsonElement> configProperty in additionalConfigProperties) {
				switch (configProperty.Key) {
					case "EnableFreePackages" when (configProperty.Value.ValueKind == JsonValueKind.True || configProperty.Value.ValueKind == JsonValueKind.False): {
						isEnabled = configProperty.Value.GetBoolean();
						bot.ArchiLogger.LogGenericInfo("Enable Free Packages : " + isEnabled.ToString());
						break;
					}
					
					case "PauseFreePackagesWhilePlaying" when (configProperty.Value.ValueKind == JsonValueKind.True || configProperty.Value.ValueKind == JsonValueKind.False): {
						pauseWhilePlaying = configProperty.Value.GetBoolean();
						bot.ArchiLogger.LogGenericInfo("Pause Free Packages While Playing : " + pauseWhilePlaying.ToString());
						break;
					}
					
					case "FarmWatchAutoClaimEnabled" when (configProperty.Value.ValueKind == JsonValueKind.True || configProperty.Value.ValueKind == JsonValueKind.False): {
						farmWatchEnabled = configProperty.Value.GetBoolean();
						bot.ArchiLogger.LogGenericInfo("FarmWatch auto-claim for this bot : " + farmWatchEnabled.ToString());
						break;
					}

					case "PauseFreePackagesWhileFarming" when (configProperty.Value.ValueKind == JsonValueKind.True || configProperty.Value.ValueKind == JsonValueKind.False): {
						pauseWhileFarming = configProperty.Value.GetBoolean();
						bot.ArchiLogger.LogGenericInfo("Pause Free Packages While Farming : " + pauseWhileFarming.ToString());
						break;
					}

					case "FreePackagesPerHour" or "FreePackagesLimit" when configProperty.Value.ValueKind == JsonValueKind.Number: {
						packageLimit = configProperty.Value.ToJsonObject<uint>();
						bot.ArchiLogger.LogGenericInfo("Free Packages Limit : " + packageLimit.ToString());
						break;
					}

					case "FreePackagesFilter": {
						FilterConfig? filter = configProperty.Value.ToJsonObject<FilterConfig>();
						if (filter != null) {
							bot.ArchiLogger.LogGenericInfo("Free Packages Filter : " + filter.ToJsonText());
							filterConfigs.Add(filter);
						}
						break;
					}
					
					case "FreePackagesFilters": {
						List<FilterConfig>? filters = configProperty.Value.ToJsonObject<List<FilterConfig>>();
						if (filters != null) {
							bot.ArchiLogger.LogGenericInfo("Free Packages Filters : " + filters.ToJsonText());
							filterConfigs.AddRange(filters);
						}
						break;
					}
				}
			}
			
			if (FarmWatchSettings.Current.BotAutoClaim.TryGetValue(bot.BotName, out bool savedBotSetting)) {
				farmWatchEnabled = savedBotSetting;
			}

			if (isEnabled) {
				await PackageHandler.AddHandler(bot, filterConfigs, packageLimit, pauseWhilePlaying, pauseWhileFarming, farmWatchEnabled).ConfigureAwait(false);
			}
		}

		public Task<uint> GetPreferredChangeNumberToStartFrom() {
			return Task.FromResult(GlobalCache?.LastChangeNumber ?? 0);
		}

		public async Task OnPICSChanges(uint currentChangeNumber, IReadOnlyDictionary<uint, SteamApps.PICSChangesCallback.PICSChangeData> appChanges, IReadOnlyDictionary<uint, SteamApps.PICSChangesCallback.PICSChangeData> packageChanges) {
			await InitializeGlobalCache().ConfigureAwait(false);
			PICSHandler.OnPICSChanges(currentChangeNumber, appChanges, packageChanges);
		}

		public async Task OnPICSChangesRestart(uint currentChangeNumber) {
			await InitializeGlobalCache().ConfigureAwait(false);
			await PICSHandler.OnPICSRestart(currentChangeNumber).ConfigureAwait(false);
		}

		public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager) {
			callbackManager.Subscribe<SteamApps.LicenseListCallback>(callback => OnLicenseList(bot, callback));

			return Task.CompletedTask;
		}

		public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot) {
			return Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(new List<ClientMsgHandler> { SteamHandler.AddHandler(bot) });
		}

		public Task OnBotFarmingStarted(Bot bot) {
			FarmWatchStatisticsStore.RecordFarmingStarted(bot.BotName, bot.CardsFarmer.GamesToFarmReadOnly);
			return Task.CompletedTask;
		}

		public Task OnBotFarmingStopped(Bot bot) {
			FarmWatchStatisticsStore.RecordFarmingStopped(bot.BotName);
			return Task.CompletedTask;
		}

		public Task OnBotFarmingFinished(Bot bot, bool farmedSomething) {
			FarmWatchStatisticsStore.RecordFarmingStopped(bot.BotName);
			return Task.CompletedTask;
		}

		public Task OnBotTradeOfferResults(Bot bot, IReadOnlyCollection<ArchiSteamFarm.Steam.Exchange.ParseTradeResult> tradeResults) {
			foreach (ArchiSteamFarm.Steam.Exchange.ParseTradeResult result in tradeResults) {
				if (result.Result != ArchiSteamFarm.Steam.Exchange.ParseTradeResult.EResult.Accepted || !result.Confirmed) continue;
				long itemsSent = result.ItemsToGive?.Sum(item => item.Amount) ?? 0;
				long itemsReceived = result.ItemsToReceive?.Sum(item => item.Amount) ?? 0;
				FarmWatchStatisticsStore.RecordAcceptedTrade(bot.BotName, result.TradeOfferID, itemsSent, itemsReceived);
			}
			return Task.CompletedTask;
		}

		private static void OnLicenseList (Bot bot, SteamApps.LicenseListCallback callback) {
			PackageHandler.OnLicenseList(bot, callback);
		}

		public async Task OnBotLoggedOn(Bot bot) {
			await PackageHandler.OnBotLoggedOn(bot).ConfigureAwait(false);
		}

		public Task OnBotDisconnected(Bot bot, EResult reason) {
			return Task.FromResult(0);
		}
	}
}
