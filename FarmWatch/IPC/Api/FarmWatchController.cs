using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
		bool configured = await ConnectUploader.Configure(request.Enabled, request.Endpoint, request.Token).ConfigureAwait(false);
		if (!configured) return BadRequest(new GenericResponse(false, "When enabling Connect, provide an HTTPS WordPress upload endpoint and a valid Bearer token."));
		FarmWatchDiagnostics.Record("connect", request.Enabled ? "WatchFarm Connect uploader enabled." : "WatchFarm Connect uploader disabled.");
		return Ok(new GenericResponse<object>(new { status = ConnectUploader.GetStatus(), message = request.Enabled ? "Connect configured. The first statistics snapshot has been queued for upload." : "Connect uploader disabled. Pending uploads are retained locally." }));
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

		string configPath = Bot.GetFilePath(bot.BotName, Bot.EFileType.Config);
		string temporaryPath = configPath + ".farmwatch.tmp";
		try {
			if (!System.IO.File.Exists(configPath)) return new ObjectResult(new GenericResponse(false, "The bot configuration file was not found.")) { StatusCode = (int) HttpStatusCode.NotFound };
			JsonNode? parsed = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(configPath).ConfigureAwait(false));
			if (parsed is not JsonObject config) return BadRequest(new GenericResponse(false, "The bot configuration JSON root is not an object."));
			bool changed = false;
			if (config["EnableFreePackages"]?.GetValue<bool>() != true) { config["EnableFreePackages"] = true; changed = true; }
			if (config["FarmWatchAutoClaimEnabled"] == null) { config["FarmWatchAutoClaimEnabled"] = true; changed = true; }
			if (changed) {
				await System.IO.File.WriteAllTextAsync(temporaryPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
				System.IO.File.Move(temporaryPath, configPath, true);
			}
			FarmWatchDiagnostics.Record("settings", "FarmWatch bot configuration added. Auto-claim uses the enabled-by-default setting.", bot.BotName, "success");
			return Ok(new GenericResponse<object>(new { configured = true, changed, restartRequired = changed, message = changed ? "FarmWatch has been added. Restart this bot to load the plugin." : "This bot is already configured for FarmWatch." }));
		} catch (Exception e) {
			try { if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath); } catch { }
			FarmWatchDiagnostics.Record("settings", "Could not configure FarmWatch for this bot.", bot.BotName, "error", e.Message);
			return BadRequest(new GenericResponse(false, "Could not update bot configuration: " + e.Message));
		}
	}

	[HttpPost("ConfigureAllBots")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public async Task<ActionResult<GenericResponse>> ConfigureAllBots() {
		int configured = 0;
		int unchanged = 0;
		List<string> failed = new();

		foreach (Bot bot in Bot.BotsReadOnly?.Values.OrderBy(bot => bot.BotName) ?? Enumerable.Empty<Bot>()) {
			string configPath = Bot.GetFilePath(bot.BotName, Bot.EFileType.Config);
			string temporaryPath = configPath + ".farmwatch.tmp";

			try {
				if (!System.IO.File.Exists(configPath)) {
					failed.Add(bot.BotName + ": config file not found");
					continue;
				}

				JsonNode? parsed = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(configPath).ConfigureAwait(false));
				if (parsed is not JsonObject config) {
					failed.Add(bot.BotName + ": config JSON root is not an object");
					continue;
				}

				bool changed = false;
				if (config["EnableFreePackages"]?.GetValue<bool>() != true) {
					config["EnableFreePackages"] = true;
					changed = true;
				}
				if (config["FarmWatchAutoClaimEnabled"] == null) {
					config["FarmWatchAutoClaimEnabled"] = true;
					changed = true;
				}

				if (!changed) {
					unchanged++;
					continue;
				}

				await System.IO.File.WriteAllTextAsync(temporaryPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
				System.IO.File.Move(temporaryPath, configPath, true);
				configured++;
				FarmWatchDiagnostics.Record("settings", "FarmWatch bot configuration added to bot JSON. Auto-claim is enabled by default for this bot.", bot.BotName, "success");
			} catch (Exception e) {
				try { if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath); } catch { }
				failed.Add(bot.BotName + ": " + e.Message);
				FarmWatchDiagnostics.Record("settings", "Could not update bot JSON for FarmWatch setup.", bot.BotName, "error", e.Message);
			}
		}

		return Ok(new GenericResponse<object>(new {
			configured,
			unchanged,
			failed,
			restartRequired = configured > 0,
			message = configured > 0 ? "FarmWatch settings were added. Restart the affected bots to load the plugin module." : "No bot configuration files needed changes."
		}));
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
