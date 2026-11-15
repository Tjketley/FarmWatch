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
		var bots = Bot.BotsReadOnly?.Values
			.OrderBy(bot => bot.BotName)
			.Select(bot => {
				bool configured = PackageHandler.Handlers.TryGetValue(bot.BotName, out PackageHandler? handler);
				bool autoClaimEnabled = handler?.FarmWatchEnabled ??
					(FarmWatchSettings.Current.BotAutoClaim.TryGetValue(bot.BotName, out bool savedSetting) && savedSetting);
				return new {
					name = bot.BotName,
					connected = bot.IsConnectedAndLoggedOn,
					configured,
					autoClaimEnabled
				};
			})
			.ToArray() ?? [];

		return Ok(new {
			name = "FarmWatch",
			version = typeof(FreePackages).Assembly.GetName().Version?.ToString() ?? "unknown",
			globalAutoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
			observeOnly = FreePackages.FarmWatchObserveOnly,
			bots
		});
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
		var bots = PackageHandler.Handlers.Values
			.OrderBy(handler => handler.Bot.BotName)
			.Select(handler => {
				var queuedPackages = handler.BotCache.Packages
					.OrderBy(package => package.Type.ToString())
					.ThenBy(package => package.ID)
					.ToArray();
				var recentActivations = handler.BotCache.Activations
					.OrderByDescending(timestamp => timestamp)
					.Take(10)
					.ToArray();

				return new {
					name = handler.Bot.BotName,
					connected = handler.Bot.IsConnectedAndLoggedOn,
					botEnabled = handler.Bot.BotConfig.Enabled,
					farmWatchAutoClaimEnabled = handler.FarmWatchEnabled,
					filterReady = handler.PackageFilter.Ready,
					queueStatus = handler.GetStatus(),
					cache = new {
						queuedPackages = queuedPackages.Length,
						packagesByType = queuedPackages.GroupBy(package => package.Type.ToString()).Select(group => new { type = group.Key, count = group.Count() }).ToArray(),
						changedApps = handler.BotCache.ChangedApps.Count,
						changedPackages = handler.BotCache.ChangedPackages.Count,
						newOwnedPackages = handler.BotCache.NewOwnedPackages.Count,
						seenPackages = handler.BotCache.SeenPackages.Count,
						ignoredApps = handler.BotCache.IgnoredApps.Count,
						waitlistedPlaytests = handler.BotCache.WaitlistedPlaytests.Count,
						promotionEvidence = handler.BotCache.PromotionEvidence.Count,
						currentClaimEvidence = queuedPackages.Count(package => handler.BotCache.HasCurrentClaimEvidence(package)),
						lastActivationUtc = recentActivations.FirstOrDefault() == default ? (DateTime?) null : recentActivations[0].ToUniversalTime(),
						activationsInCache = handler.BotCache.Activations.Count
					},
					pendingChanges = new {
						appIds = handler.BotCache.ChangedApps.OrderBy(id => id).Take(20).ToArray(),
						packageIds = handler.BotCache.ChangedPackages.OrderBy(id => id).Take(20).ToArray()
					},
					queue = queuedPackages.Take(30).Select(package => new {
						type = package.Type.ToString(),
						id = package.ID,
						filterHashPresent = package.FilterHash != null,
						hasCurrentClaimEvidence = handler.BotCache.HasCurrentClaimEvidence(package)
					}).ToArray()
				};
			})
			.ToArray();

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
