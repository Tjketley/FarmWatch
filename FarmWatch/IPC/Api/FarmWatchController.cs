using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ArchiSteamFarm.IPC.Controllers.Api;
using Microsoft.AspNetCore.Mvc;

namespace FreePackages.IPC;

[Route("Api/FarmWatch")]
public sealed class FarmWatchController : ArchiController {
	[HttpGet("Status")]
	[ProducesResponseType((int) HttpStatusCode.OK)]
	public ActionResult<object> GetStatus() {
		var bots = PackageHandler.Handlers.Values
			.OrderBy(handler => handler.Bot.BotName)
			.Select(handler => new {
				name = handler.Bot.BotName,
				connected = handler.Bot.IsConnectedAndLoggedOn,
				autoClaimEnabled = handler.FarmWatchEnabled
			})
			.ToArray();

		return Ok(new {
			name = "FarmWatch",
			version = typeof(FreePackages).Assembly.GetName().Version?.ToString() ?? "unknown",
			globalAutoClaimEnabled = FreePackages.FarmWatchAutoClaimEnabled,
			observeOnly = FreePackages.FarmWatchObserveOnly,
			bots
		});
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
			limit = safeLimit,
			retainedEvents = FarmWatchDiagnostics.GetRecent(500).Length,
			events = FarmWatchDiagnostics.GetRecent(safeLimit)
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
