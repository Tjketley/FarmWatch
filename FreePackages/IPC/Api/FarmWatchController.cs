using System.Linq;
using System.Net;
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
			handler.SetFarmWatchEnabled(request.BotAutoClaimEnabled.Value);
		}

		if (request.AutoClaimEnabled != null || request.ObserveOnly != null) {
			bool autoClaim = request.AutoClaimEnabled ?? FreePackages.FarmWatchAutoClaimEnabled;
			bool observeOnly = request.ObserveOnly ?? FreePackages.FarmWatchObserveOnly;
			await FarmWatchSettings.Current.SetGlobal(request.AutoClaimEnabled, request.ObserveOnly).ConfigureAwait(false);
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
