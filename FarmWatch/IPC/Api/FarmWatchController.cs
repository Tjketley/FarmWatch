using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ArchiSteamFarm.IPC.Controllers.Api;
using ArchiSteamFarm.Steam;
using Microsoft.AspNetCore.Mvc;

namespace FreePackages.IPC;

[Route("Api/FarmWatch")]
public sealed class FarmWatchController : ArchiController {
	[HttpGet("Status")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<object> GetStatus() {
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
		return Ok(new {
			name = "FarmWatch",
			version = typeof(FreePackages).Assembly.GetName().Version?.ToString() ?? "unknown",
			globalAutoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
			observeOnly = FreePackages.FarmWatchObserveOnly,
			bots
		});
	}
	[HttpGet("Statistics")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<object> GetStatistics([FromQuery] int limit = 50) {
		return Ok(FarmWatchStatisticsStore.GetDashboardStatistics(Math.Clamp(limit, 1, 200)));
	}

	[HttpGet("ConnectStatus")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<object> GetConnectStatus() => Ok(ConnectUploader.GetStatus());

	[HttpPost("Connect")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	public async Task<ActionResult<object>> ConfigureConnect([FromBody] FarmWatchConnectRequest request) {
		if (request == null) return BadRequest(new { error = "A Connect configuration is required." });
		bool configured = await ConnectUploader.Configure(request.Enabled, request.Endpoint, request.Token).ConfigureAwait(false);
		if (!configured) return BadRequest(new { error = "When enabling Connect, provide an HTTPS WordPress upload endpoint and a valid Bearer token." });
		FarmWatchDiagnostics.Record("connect", request.Enabled ? "WatchFarm Connect uploader enabled." : "WatchFarm Connect uploader disabled.");
		return Ok(new { status = ConnectUploader.GetStatus(), message = request.Enabled ? "Connect configured. The first statistics snapshot has been queued for upload." : "Connect uploader disabled. Pending uploads are retained locally." });
	}

	[HttpGet("Diagnostics")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<object> GetDiagnostics() {
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
		bots.Sort((left, right) => string.Compare(
			(string?) left.GetType().GetProperty("name")?.GetValue(left),
			(string?) right.GetType().GetProperty("name")?.GetValue(right),
			StringComparison.OrdinalIgnoreCase));

		return Ok(new {
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
		});
	}
	[HttpGet("Events")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<object> GetEvents([FromQuery] int limit = 200) {
		int safeLimit = Math.Clamp(limit, 1, 500);
		return Ok(new {
			generatedAtUtc = DateTime.UtcNow,
			persistent = FarmWatchDiagnostics.IsPersistent,
			storageFile = FarmWatchDiagnostics.StorageFileName,
			limit = safeLimit,
			retainedEvents = FarmWatchDiagnostics.GetRecent(500).Length,
			events = FarmWatchDiagnostics.GetRecent(safeLimit)
		});
	}


	[HttpPost("ConfigureBot")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	[ProducesResponseType((int) HttpStatusCode.NotFound)]
	public async Task<ActionResult<object>> ConfigureBot([FromBody] FarmWatchConfigureBotRequest request) {
		if (request == null || string.IsNullOrWhiteSpace(request.BotName)) return BadRequest(new { error = "A bot name is required." });
		Bot? bot = Bot.BotsReadOnly?.Values.FirstOrDefault(item => item.BotName.Equals(request.BotName, StringComparison.OrdinalIgnoreCase));
		if (bot == null) return NotFound(new { error = "The requested ASF bot was not found." });

		string configPath = Bot.GetFilePath(bot.BotName, Bot.EFileType.Config);
		string temporaryPath = configPath + ".farmwatch.tmp";
		try {
			if (!System.IO.File.Exists(configPath)) return NotFound(new { error = "The bot configuration file was not found." });
			JsonNode? parsed = JsonNode.Parse(await System.IO.File.ReadAllTextAsync(configPath).ConfigureAwait(false));
			if (parsed is not JsonObject config) return BadRequest(new { error = "The bot configuration JSON root is not an object." });
			bool changed = false;
			if (config["EnableFreePackages"]?.GetValue<bool>() != true) { config["EnableFreePackages"] = true; changed = true; }
			if (config["FarmWatchAutoClaimEnabled"] == null) { config["FarmWatchAutoClaimEnabled"] = false; changed = true; }
			if (changed) {
				await System.IO.File.WriteAllTextAsync(temporaryPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
				System.IO.File.Move(temporaryPath, configPath, true);
			}
			FarmWatchDiagnostics.Record("settings", "FarmWatch bot configuration added. Auto-claim remains disabled until explicitly enabled.", bot.BotName, "success");
			return Ok(new { configured = true, changed, restartRequired = changed, message = changed ? "FarmWatch has been added. Restart this bot to load the plugin." : "This bot is already configured for FarmWatch." });
		} catch (Exception e) {
			try { if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath); } catch { }
			FarmWatchDiagnostics.Record("settings", "Could not configure FarmWatch for this bot.", bot.BotName, "error", e.Message);
			return BadRequest(new { error = "Could not update bot configuration: " + e.Message });
		}
	}

	[HttpPost("ConfigureAllBots")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public async Task<ActionResult<object>> ConfigureAllBots() {
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
					config["FarmWatchAutoClaimEnabled"] = false;
					changed = true;
				}

				if (!changed) {
					unchanged++;
					continue;
				}

				await System.IO.File.WriteAllTextAsync(temporaryPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
				System.IO.File.Move(temporaryPath, configPath, true);
				configured++;
				FarmWatchDiagnostics.Record("settings", "FarmWatch bot configuration added to bot JSON. Auto-claim remains disabled for this bot until explicitly enabled.", bot.BotName, "success");
			} catch (Exception e) {
				try { if (System.IO.File.Exists(temporaryPath)) System.IO.File.Delete(temporaryPath); } catch { }
				failed.Add(bot.BotName + ": " + e.Message);
				FarmWatchDiagnostics.Record("settings", "Could not update bot JSON for FarmWatch setup.", bot.BotName, "error", e.Message);
			}
		}

		return Ok(new {
			configured,
			unchanged,
			failed,
			restartRequired = configured > 0,
			message = configured > 0 ? "FarmWatch settings were added. Restart the affected bots to load the plugin module." : "No bot configuration files needed changes."
		});
	}

	[HttpPost("Settings")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	[ProducesResponseType((int) HttpStatusCode.BadRequest)]
	[ProducesResponseType((int) HttpStatusCode.NotFound)]
	public async Task<ActionResult<object>> UpdateSettings([FromBody] FarmWatchSettingsRequest request) {
		if (request == null || (request.AutoClaimEnabled == null && request.ObserveOnly == null && (request.BotName == null || request.BotAutoClaimEnabled == null))) {
			return BadRequest(new { error = "No settings were supplied." });
		}

		if (request.BotName != null) {
			if (request.BotAutoClaimEnabled == null || !PackageHandler.Handlers.TryGetValue(request.BotName, out PackageHandler? handler)) {
				return NotFound(new { error = "FarmWatch bot was not found or its setting was not supplied." });
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

		return Ok(new {
			globalAutoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
			observeOnly = FreePackages.FarmWatchObserveOnly,
			message = "FarmWatch settings saved."
		});
	}
}

public sealed class FarmWatchSettingsRequest {
	public bool? AutoClaimEnabled { get; set; }
	public bool? ObserveOnly { get; set; }
	public string? BotName { get; set; }
	public bool? BotAutoClaimEnabled { get; set; }
}

public sealed class FarmWatchConnectRequest {
	public bool Enabled { get; set; }
	public string? Endpoint { get; set; }
	public string? Token { get; set; }
}

public sealed class FarmWatchConfigureBotRequest {
	public string? BotName { get; set; }
}
