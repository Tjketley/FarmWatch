using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.IPC.Controllers.Api;
using ArchiSteamFarm.IPC.Responses;
using ArchiSteamFarm.Steam;
using Microsoft.AspNetCore.Mvc;

namespace FreePackages.IPC;

[Route("/Api/FarmWatch")]
public sealed class FarmWatchController : ArchiController {
	[HttpGet("Status")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<GenericResponse> GetStatus() {
		List<object> bots = new();
		if (Bot.BotsReadOnly != null) {
			foreach (Bot bot in Bot.BotsReadOnly.Values) {
				bool configured = PackageHandler.Handlers.TryGetValue(bot.BotName, out PackageHandler? handler);
				bool autoClaimEnabled = handler?.FarmWatchEnabled ??
					(FarmWatchSettings.Current.BotAutoClaim.TryGetValue(bot.BotName, out bool savedSetting) && savedSetting);
				bots.Add(new {
					name = bot.BotName,
					connected = bot.IsConnectedAndLoggedOn,
					configured,
					autoClaimEnabled
				});
			}
		}
		return Ok(new GenericResponse<object>(new {
			name = "FarmWatch",
			version = typeof(FreePackages).Assembly.GetName().Version?.ToString() ?? "unknown",
			globalAutoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
			observeOnly = FreePackages.FarmWatchObserveOnly,
			bots
		}));
	}
	[HttpGet("Statistics")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<GenericResponse> GetStatistics([FromQuery] int limit = 50) {
		return Ok(new GenericResponse<object>(FarmWatchStatisticsStore.GetDashboardStatistics(Math.Clamp(limit, 1, 200))));
	}

	[HttpPost("TradeBaseline")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	public async Task<ActionResult<GenericResponse>> SetTradeBaseline([FromBody] FarmWatchTradeBaselineRequest request) {
		if (request == null || request.CompletedTrades < 0 || request.CompletedTrades > 1_000_000_000) {
			return BadRequest(new GenericResponse(false, "Enter a completed-trade count between 0 and 1,000,000,000."));
		}
		await FarmWatchSettings.Current.SetManualTradeBaseline(request.CompletedTrades).ConfigureAwait(false);
		FarmWatchDiagnostics.Record("analytics", $"Pre-monitoring trade baseline set to {request.CompletedTrades}.");
		return Ok(new GenericResponse<object>(new { manualBaseline = FarmWatchSettings.Current.ManualTradeBaseline, message = "Pre-monitoring trade total saved." }));
	}

	[HttpGet("ConnectStatus")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<GenericResponse> GetConnectStatus() => Ok(new GenericResponse<object>(ConnectUploader.GetStatus()));

	[HttpPost("Connect")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	public async Task<ActionResult<GenericResponse>> ConfigureConnect([FromBody] FarmWatchConnectRequest request) {
		if (request == null) return BadRequest(new GenericResponse(false, "A Connect configuration is required."));
		try {
			bool configured = await ConnectUploader.Configure(request.Enabled, request.Endpoint, request.Token).ConfigureAwait(false);
			if (!configured) return BadRequest(new GenericResponse(false, "Invalid Connect configuration. Enabling requires an HTTPS endpoint without query/fragment/user-info and a 32-128 character Bearer token using letters, numbers, underscore or hyphen."));
			FarmWatchDiagnostics.Record("connect", request.Enabled ? "WatchFarm Connect uploader enabled." : "WatchFarm Connect uploader disabled.");
			return Ok(new GenericResponse<object>(new { status = ConnectUploader.GetStatus(), message = request.Enabled ? "Connect configured. The first statistics snapshot has been queued for upload." : "Connect uploader disabled. Pending uploads are retained locally." }));
		} catch (Exception e) {
			string errorId = Guid.NewGuid().ToString("N");
			ASF.ArchiLogger.LogGenericWarningException(e);
			FarmWatchDiagnostics.Record("connect", "Connect configuration failed. Reference: " + errorId, level: "error", details: e.ToString());
			return new ObjectResult(new GenericResponse(false, "Connect configuration failed. Reference: " + errorId + ". Check FarmWatch Events/ASF logs for the exception.")) { StatusCode = (int) HttpStatusCode.InternalServerError };
		}
	}

	[HttpGet("Diagnostics")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<GenericResponse> GetDiagnostics() {
		List<object> bots = new();
		foreach (PackageHandler handler in PackageHandler.Handlers.Values) {
			List<Package> queuedPackages = new(handler.BotCache.Packages);
			queuedPackages.Sort((left, right) => {
				int typeOrder = string.Compare(left.Type.ToString(), right.Type.ToString(), StringComparison.Ordinal);
				return typeOrder != 0 ? typeOrder : left.ID.CompareTo(right.ID);
			});

			List<DateTime> recentActivations = new(handler.BotCache.Activations);
			recentActivations.Sort((left, right) => right.CompareTo(left));
			if (recentActivations.Count > 10) recentActivations.RemoveRange(10, recentActivations.Count - 10);

			List<uint> appIds = new(handler.BotCache.ChangedApps);
			List<uint> packageIds = new(handler.BotCache.ChangedPackages);
			appIds.Sort();
			packageIds.Sort();
			if (appIds.Count > 20) appIds.RemoveRange(20, appIds.Count - 20);
			if (packageIds.Count > 20) packageIds.RemoveRange(20, packageIds.Count - 20);

			List<object> queue = new();
			Dictionary<string, int> packagesByType = new(StringComparer.Ordinal);
			int currentClaimEvidence = 0;
			for (int i = 0; i < queuedPackages.Count; i++) {
				Package package = queuedPackages[i];
				string type = package.Type.ToString();
				packagesByType[type] = packagesByType.TryGetValue(type, out int count) ? count + 1 : 1;
				if (handler.BotCache.HasCurrentClaimEvidence(package)) currentClaimEvidence++;
				if (i < 30) {
					queue.Add(new {
						type,
						id = package.ID,
						filterHashPresent = package.FilterHash != null,
						hasCurrentClaimEvidence = handler.BotCache.HasCurrentClaimEvidence(package)
					});
				}
			}

			List<object> packageTypeSummary = new();
			foreach (KeyValuePair<string, int> entry in packagesByType) {
				packageTypeSummary.Add(new { type = entry.Key, count = entry.Value });
			}

			bots.Add(new {
				name = handler.Bot.BotName,
				connected = handler.Bot.IsConnectedAndLoggedOn,
				botEnabled = handler.Bot.BotConfig.Enabled,
				farmWatchAutoClaimEnabled = handler.FarmWatchEnabled,
				filterReady = handler.PackageFilter.Ready,
				queueStatus = handler.GetStatus(),
				cache = new {
					queuedPackages = queuedPackages.Count,
					packagesByType = packageTypeSummary,
					changedApps = handler.BotCache.ChangedApps.Count,
					changedPackages = handler.BotCache.ChangedPackages.Count,
					newOwnedPackages = handler.BotCache.NewOwnedPackages.Count,
					seenPackages = handler.BotCache.SeenPackages.Count,
					ignoredApps = handler.BotCache.IgnoredApps.Count,
					waitlistedPlaytests = handler.BotCache.WaitlistedPlaytests.Count,
					promotionEvidence = handler.BotCache.PromotionEvidence.Count,
					currentClaimEvidence,
					lastActivationUtc = recentActivations.Count == 0 ? (DateTime?) null : recentActivations[0].ToUniversalTime(),
					activationsInCache = handler.BotCache.Activations.Count
				},
				pendingChanges = new { appIds, packageIds },
				queue
			});
		}

		return Ok(new GenericResponse<object>(new {
			name = "FarmWatch",
			version = typeof(FreePackages).Assembly.GetName().Version?.ToString() ?? "unknown",
			generatedAtUtc = DateTime.UtcNow,
			global = new {
				autoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
				observeOnly = FreePackages.FarmWatchObserveOnly,
				lastPicsChangeNumber = FreePackages.GlobalCache?.LastChangeNumber ?? 0,
				handlerCount = PackageHandler.Handlers.Count
			},
			bots
		}));
	}
	[HttpGet("Events")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<GenericResponse> GetEvents([FromQuery] int limit = 200) {
		int safeLimit = Math.Clamp(limit, 1, 500);
		return Ok(new GenericResponse<object>(new {
			generatedAtUtc = DateTime.UtcNow,
			persistent = FarmWatchDiagnostics.IsPersistent,
			storageFile = FarmWatchDiagnostics.StorageFileName,
			limit = safeLimit,
			retainedEvents = FarmWatchDiagnostics.GetRecent(500).Length,
			events = FarmWatchDiagnostics.GetRecent(safeLimit)
		}));
	}


	[HttpPost("ConfigureBot")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	[ProducesResponseType((int) HttpStatusCode.NotFound)]
	public async Task<ActionResult<GenericResponse>> ConfigureBot([FromBody] FarmWatchConfigureBotRequest request) {
		if (request == null || string.IsNullOrWhiteSpace(request.BotName)) return BadRequest(new GenericResponse(false, "A bot name is required."));
		Bot? bot = Bot.BotsReadOnly?.Values.FirstOrDefault(item => item.BotName.Equals(request.BotName, StringComparison.OrdinalIgnoreCase));
		if (bot == null) return new ObjectResult(new GenericResponse(false, "The requested ASF bot was not found.")) { StatusCode = (int) HttpStatusCode.NotFound };

		(bool success, bool changed, bool restarted, string message) = await ConfigureBotSafely(bot).ConfigureAwait(false);
		if (!success) return BadRequest(new GenericResponse(false, message));
		return Ok(new GenericResponse<object>(new { configured = true, changed, restarted, restartRequired = false, message }));
	}

	[HttpPost("ConfigureAllBots")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public async Task<ActionResult<GenericResponse>> ConfigureAllBots() {
		int configured = 0, unchanged = 0, restarted = 0;
		List<object> results = new();
		foreach (Bot bot in Bot.BotsReadOnly?.Values.OrderBy(bot => bot.BotName) ?? Enumerable.Empty<Bot>()) {
			(bool success, bool changed, bool wasRestarted, string message) = await ConfigureBotSafely(bot).ConfigureAwait(false);
			if (success) {
				if (changed) configured++; else unchanged++;
				if (wasRestarted) restarted++;
			}
			results.Add(new { bot = bot.BotName, success, changed, restarted = wasRestarted, message });
		}
		return Ok(new GenericResponse<object>(new {
			configured, unchanged, restarted, results,
			message = $"Configured {configured} bot(s); {unchanged} already configured; restarted {restarted} running bot(s)."
		}));
	}

	private static async Task<(bool Success, bool Changed, bool Restarted, string Message)> ConfigureBotSafely(Bot bot) {
		string configPath = Bot.GetFilePath(bot.BotName, Bot.EFileType.Config);
		string temporaryPath = configPath + ".farmwatch.tmp";
		string backupPath = configPath + ".farmwatch-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
		bool wasRunning = bot.KeepRunning;
		bool stopped = false, replaced = false, changed = false;

		try {
			if (!File.Exists(configPath)) return (false, false, false, "Bot configuration file was not found.");
			JsonNode? parsed = JsonNode.Parse(await File.ReadAllTextAsync(configPath).ConfigureAwait(false));
			if (parsed is not JsonObject config) return (false, false, false, "Bot configuration JSON root is not an object.");

			if (config["EnableFreePackages"] is JsonValue enableValue && enableValue.TryGetValue<bool>(out bool enabled)) {
				if (!enabled) { config["EnableFreePackages"] = true; changed = true; }
			} else if (config["EnableFreePackages"] == null) {
				config["EnableFreePackages"] = true; changed = true;
			} else {
				return (false, false, false, "EnableFreePackages exists but is not a Boolean. No changes were made.");
			}

			if (config["FarmWatchAutoClaimEnabled"] == null) {
				config["FarmWatchAutoClaimEnabled"] = true; changed = true;
			} else if (config["FarmWatchAutoClaimEnabled"] is not JsonValue autoClaimValue || !autoClaimValue.TryGetValue<bool>(out _)) {
				return (false, false, false, "FarmWatchAutoClaimEnabled exists but is not a Boolean. No changes were made.");
			}

			if (!changed && PackageHandler.Handlers.ContainsKey(bot.BotName)) {
				return (true, false, false, "Already configured; no restart needed.");
			}

			// Stop the live bot before touching its configuration file.
			if (wasRunning) {
				(bool stopSuccess, string stopMessage) = await bot.Actions.Stop().ConfigureAwait(false);
				if (!stopSuccess) return (false, false, false, "Could not stop bot: " + stopMessage);
				stopped = true;
			}

			File.Copy(configPath, backupPath, false);
			if (changed) {
				await File.WriteAllTextAsync(temporaryPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
				_ = JsonNode.Parse(await File.ReadAllTextAsync(temporaryPath).ConfigureAwait(false)) ?? throw new InvalidDataException("Updated bot config could not be validated.");
				File.Move(temporaryPath, configPath, true);
				replaced = true;
			}

			Dictionary<string, JsonElement> properties = new(StringComparer.OrdinalIgnoreCase);
			using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath).ConfigureAwait(false))) {
				foreach (JsonProperty property in document.RootElement.EnumerateObject()) properties[property.Name] = property.Value.Clone();
			}
			await FreePackages.InitializeBotModule(bot, properties).ConfigureAwait(false);
			if (!PackageHandler.Handlers.ContainsKey(bot.BotName)) throw new InvalidOperationException("FarmWatch module did not initialise. Check EnableFreePackages and the bot config.");

			bool restarted = false;
			if (wasRunning) {
				(bool startSuccess, string startMessage) = bot.Actions.Start();
				if (!startSuccess) throw new InvalidOperationException("Configuration saved, but ASF could not restart the bot: " + startMessage);
				restarted = true;
			}
			FarmWatchDiagnostics.Record("settings", changed ? "Bot config updated; FarmWatch module reinitialised and bot restarted." : "FarmWatch module reinitialised for existing bot config.", bot.BotName, "success");
			return (true, changed, restarted, changed ? (restarted ? "Configured, reinitialised and restarted." : "Configured and reinitialised; bot was already stopped.") : "Already configured; FarmWatch module is active.");
		} catch (Exception e) {
			try {
				if (replaced && File.Exists(backupPath)) File.Copy(backupPath, configPath, true);
				if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
				if (replaced && File.Exists(configPath)) {
					Dictionary<string, JsonElement> original = new(StringComparer.OrdinalIgnoreCase);
					using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(configPath).ConfigureAwait(false));
					foreach (JsonProperty property in document.RootElement.EnumerateObject()) original[property.Name] = property.Value.Clone();
					await FreePackages.InitializeBotModule(bot, original).ConfigureAwait(false);
				}
			} catch (Exception rollbackError) {
				ASF.ArchiLogger.LogGenericWarningException(rollbackError);
			}
			if (stopped) {
				try { _ = bot.Actions.Start(); } catch (Exception restartError) { ASF.ArchiLogger.LogGenericWarningException(restartError); }
			}
			FarmWatchDiagnostics.Record("settings", "Bot configuration failed; rollback attempted.", bot.BotName, "error", e.ToString());
			return (false, false, false, "Could not configure bot: " + e.Message);
		}
	}

	[HttpPost("Settings")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	[ProducesResponseType((int) HttpStatusCode.NotFound)]
	public async Task<ActionResult<GenericResponse>> UpdateSettings([FromBody] FarmWatchSettingsRequest request) {
		if (request == null || (request.AutoClaimEnabled == null && request.ObserveOnly == null && (request.BotName == null || request.BotAutoClaimEnabled == null))) {
			return BadRequest(new GenericResponse(false, "No settings were supplied."));
		}

		if (request.BotName != null) {
			if (request.BotAutoClaimEnabled == null || !PackageHandler.Handlers.TryGetValue(request.BotName, out PackageHandler? handler)) {
				return new ObjectResult(new GenericResponse(false, "FarmWatch bot was not found or its setting was not supplied.")) { StatusCode = (int) HttpStatusCode.NotFound };
			}

			await FarmWatchSettings.Current.SetBot(request.BotName, request.BotAutoClaimEnabled.Value).ConfigureAwait(false);
			FarmWatchDiagnostics.Record("settings", $"Bot auto-claim set to {request.BotAutoClaimEnabled.Value}.", request.BotName, request.BotAutoClaimEnabled.Value ? "warning" : "info");
			handler.SetFarmWatchEnabled(request.BotAutoClaimEnabled.Value);
		}

		if (request.AutoClaimEnabled != null || request.ObserveOnly != null) {
			bool autoClaim = request.AutoClaimEnabled ?? FreePackages.FarmWatchAutoClaimEnabled;
			bool observeOnly = request.ObserveOnly ?? FreePackages.FarmWatchObserveOnly;
			await FarmWatchSettings.Current.SetGlobal(request.AutoClaimEnabled, request.ObserveOnly).ConfigureAwait(false);
			FarmWatchDiagnostics.Record("settings", $"Global settings updated: autoClaim={autoClaim}, observeOnly={observeOnly}.", level: autoClaim && !observeOnly ? "warning" : "info");
			FreePackages.ApplyGlobalSettings(autoClaim, observeOnly);
		}

		return Ok(new GenericResponse<object>(new {
			globalAutoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
			observeOnly = FreePackages.FarmWatchObserveOnly,
			message = "FarmWatch settings saved."
		}));
	}
}

public sealed class FarmWatchSettingsRequest {
	public bool? AutoClaimEnabled { get; set; }
	public bool? ObserveOnly { get; set; }
	public string? BotName { get; set; }
	public bool? BotAutoClaimEnabled { get; set; }
}

public sealed class FarmWatchTradeBaselineRequest {
	public long CompletedTrades { get; set; }
}

public sealed class FarmWatchConnectRequest {
	public bool Enabled { get; set; }
	public string? Endpoint { get; set; }
	public string? Token { get; set; }
}

public sealed class FarmWatchConfigureBotRequest {
	public string? BotName { get; set; }
}
