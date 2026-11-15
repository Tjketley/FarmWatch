using System.Linq;
using System.Net;
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
}
