<p align="center"><img src="assets/farmwatch-mark.svg" alt="FarmWatch eye and shield logo" width="150"></p>

# FarmWatch

<p align="center">
  <a href="https://github.com/Tjketley/FarmWatch-2/releases">Releases</a> ·
  <a href="https://tjketley.com">Thomas Ketley / Project Site</a> ·
  <a href="FarmWatch/CONNECT.md">FarmWatch Connect</a> ·
  <a href="https://github.com/Tjketley/FarmWatch-2/issues">Report an issue</a>
</p>

**Steam Free-to-Keep monitoring and guarded licence claiming for ArchiSteamFarm.**

FarmWatch watches Steam's Product Information and Change Service (PICS) for promotion changes, verifies whether a package is genuinely being offered as a permanent Free-to-Keep licence, and can optionally claim eligible promotions for selected ASF bots.

It is designed around one rule: **discover broadly, verify carefully, claim only when every safety check agrees.**

<p align="center">
  <strong>NEON GREEN · DEEP NAVY · FARMWATCH</strong><br>
  <em>Independent ASF plugin · Raspberry Pi friendly · Open source</em>
</p>

 > **ALPHA SOFTWARE — NOT PRODUCTION READY.** FarmWatch is experimental and may contain bugs, incomplete features, or data-migration issues. Use a separate test ASF installation and test bot wherever possible; back up ASF data before installing or upgrading. Verify every safety setting before connecting a real account. New installations currently default to automatic claiming enabled, so switch it off explicitly if you only want to observe promotions. Do not rely on FarmWatch as the sole safeguard against unintended Steam licence activations.

## What FarmWatch does

- **Promotion monitoring** — listens for Steam PICS changes and evaluates newly changed app and package metadata.
- **Strict Free-to-Keep eligibility** — requires explicit Steam free-promotion evidence, NoCost billing, a SinglePurchase licence, one eligible game, and current availability.
- **Fresh verification before claiming** — fetches package metadata again immediately before activation and requires matching, unexpired evidence no older than five minutes.
- **Observe-only mode** — discovers and verifies eligible candidates without activating licences. This is the default operating mode.
- **Optional per-bot claiming** — global and per-bot switches must all explicitly permit activation.
- **Owned-game free DLC** — handled through a separate evidence path that requires a free DLC classification and a verified parent game already owned by that bot.
- **Premium ASF web dashboard** — bundled inside the FarmWatch plugin and served automatically by ASF through its native plugin web-interface integration. It preserves ASF's native bots, commands, logs, configuration and administration features, with FarmWatch overview, analytics, safety and diagnostics pages.
- **Persistent settings** — FarmWatch-owned settings and data are stored under `plugins/FarmWatch/`, separate from ASF bot configuration.
- **ASF-native update support** — implements ASF's GitHub plugin update interface so ASF can discover published FarmWatch releases.
- **Existing ASF workflows** — retains compatible legacy FreePackages configuration keys and commands during the transition.

## Safety model

FarmWatch is deliberately fail-closed.

| Control | Default | Purpose |
|---|---:|---|
| Observe-only | **Off** | Monitor and verify without claiming |
| Global auto-claim | **On** for new installs | Master permission for automatic claims |
| Per-bot auto-claim | **On** for new installs | Explicit permission for each bot |

A Free-to-Keep activation is permitted only when all of the following are true:

1. Observe-only is disabled.
2. The global auto-claim master switch is enabled.
3. The relevant bot's auto-claim switch is enabled.
4. Steam metadata confirms a permanent, no-cost, single-purchase promotion for one eligible game.
5. A fresh metadata recheck and current evidence pass the final activation gate.

**Not eligible for the Free-to-Keep claim path:** free weekends, demos, playtests, temporary access, free-to-play catalogue apps, multi-app bundles, paid packages, and DLC giveaways presented as base-game promotions.

Free DLC for an already-owned game is a separate, narrowly verified path. Its evidence cannot be used to authorise a base-game Free-to-Keep claim.

Turning on a dashboard switch does not bypass these checks. Missing or invalid evidence means no claim.

## Dashboard

The FarmWatch dashboard is served by ASF's own web server at the normal web root. The UI files are included in the plugin ZIP under the plugin's `www/` directory. FarmWatch implements ASF's `IWebInterface` contract, so ASF serves those files automatically without copying over ASF's own installation directory. The UI is built from the ASF-ui revision pinned to ASF 6.3.11.7 and retains ASF's native interface routes and controls.

FarmWatch's API remains inside the plugin and is accessed through ASF's authenticated IPC interface. The UI package does not add a separate web server, reverse tunnel or public control plane. Keep ASF's IPC/web interface protected and do not expose it directly to the public internet without appropriate secure access controls.

## Requirements

- ArchiSteamFarm **6.3.11.7** (the current development target; later compatibility will be documented when tested).
- A supported ASF installation with plugin support and its IPC/web interface enabled.
- A Steam bot configured and connected in ASF.
- .NET runtime requirements matching the ASF build you use.

FarmWatch is being developed and tested against ASF-generic on 64-bit ARM Raspberry Pi OS as well as the CI build environment. Other ASF variants should be treated as unverified until tested.

## Installation

**No public release is available yet.** Once the first release is published, installation will be:

1. Download `FarmWatch-<version>.zip` from the GitHub release.
2. Extract it into its own folder under ASF's `plugins` directory (for example, `plugins/FarmWatch/`). The ZIP already contains the matching UI in `www/`.
3. Restart ASF and confirm the FarmWatch plugin loads.
4. Open ASF at its normal web interface URL. FarmWatch is available from the sidebar.

Do not replace or delete your existing ASF configuration, bot files, or databases during installation. FarmWatch keeps its own settings and per-bot cache separate.

## Updating

FarmWatch implements ASF's `IGitHubPluginUpdates` integration and points to this repository. ASF's plugin update mechanism downloads one ZIP containing both the plugin DLL and its `www/` UI assets. The UI therefore updates automatically with the plugin; there is no separate UI package or manual replacement step.

The release workflow builds, tests and packages the plugin and bundled UI together in one ZIP when a version tag is pushed. **Automatic updating is not considered verified until a real plugin update and matching UI replacement have been tested on the target Raspberry Pi.** Until then, treat updates as a planned capability rather than a completed end-to-end test.

## Configuration and compatibility

The dashboard is the intended user-facing place to manage FarmWatch's safety switches. Optional outbound statistics reporting to the user's WordPress site is documented in [FarmWatch Connect](FarmWatch/CONNECT.md); it is disabled by default. Legacy FreePackages keys and commands remain in the code for compatibility with existing setups. They are not the preferred way to configure new FarmWatch installations.

FarmWatch stores its settings and persistent data in `plugins/FarmWatch/`, keeping ASF's root `config/` directory for bot configuration. Do not edit FarmWatch files while ASF is running; use the dashboard so changes are validated, applied, and saved together.

## Privacy

FarmWatch uses Steam/ASF data needed to discover promotions, check package metadata, and verify ownership for the separate free-DLC path. The FarmWatch dashboard is hosted locally by ASF. It does not send Steam credentials, cookies, session identifiers, or account library details to a FarmWatch-operated website. Any future companion website integration is intended to use aggregate statistics only.

### Localization

FarmWatch currently ships English only. The neutral `Strings.resx` is the sole localization resource; translated satellite resource files are intentionally excluded to keep the plugin package lean and avoid ASF recursively loading duplicate satellite assemblies.

## 2.0.0.16 API response compatibility

**2.0.0.16 is an investigation build, not a beta or production release.** FarmWatch API success and error responses now use ASF's standard `GenericResponse` envelope (`Success`, `Message`, and `Result`). ASF-ui's shared HTTP client requires this envelope and rejects raw JSON objects even when the HTTP status is 200; this caused Overview, Analytics, Safety and Diagnostics to display load failures while direct endpoint checks returned HTTP 200. The release also makes the optional ASF wallet-currency schema lookup non-fatal when the Swagger schema does not expose the expected enum definition.

Validation must confirm the browser receives `Success: true` and a populated `Result` for Status, Statistics, Diagnostics, Events and ConnectStatus; POST Settings, ConfigureBot, ConfigureAllBots, TradeBaseline and Connect must return the same envelope on success and ASF-compatible error envelopes on failure. Verify the dashboard in a clean browser session on ASF 6.3.11.7 ARM64, including settings persistence across restart. CI success does not replace the Raspberry Pi test.

## 2.0.0.14 runtime compatibility work

- Replaced Type-based `System.Text.Json` overloads with generic overloads so serialization resolves through FarmWatch's source-generated context on the System.Text.Json runtime hosted by ASF 6.3.11.7.
- Removed the explicit `HttpClientHandler.AllowAutoRedirect` setter from ConnectUploader because that setter is unavailable in the runtime shown by the Raspberry Pi smoke-test logs.
- This is a compatibility test build. Raspberry Pi validation is still required; Connect's redirect behaviour now uses the default HttpClient handler.

## Current alpha validation notes

**2.0.0.14 is an investigation build, not a beta or production release.** The package workflow no longer copies culture-specific `FarmWatch.resources.dll` satellite assemblies into the plugin directory. ASF 6.3.11.7 recursively enumerates every `.dll` under `plugins/` and calls `Assembly.LoadFrom`; the satellite assembly was therefore picked up as a plugin assembly even though .NET may already have loaded it for resource lookup, producing a duplicate assembly-name load warning/error. The release workflow now fails if any `*.resources.dll` is present in the plugin package.

This intentionally prioritizes reliable plugin loading over the optional satellite translation assembly. Verify the generated ZIP contains only the main FarmWatch assembly plus non-DLL content, then test startup on ASF 6.3.11.7 ARM64.


**2.0.0.13 is an investigation build, not a beta or production release.** The FarmWatch API controller now declares an absolute `/Api/FarmWatch` route so it does not accidentally combine with ASF's inherited `/Api` controller route. The bundled ASF UI already prefixes API requests with `/api`, so its FarmWatch requests should resolve to this controller.

The plugin package deliberately excludes culture-specific `FarmWatch.resources.dll` satellite assemblies. ASF 6.3.11.7 recursively scans every DLL under `plugins/` and loads each through `Assembly.LoadFrom`; satellite assemblies can therefore collide with resource assemblies already resolved by .NET. FarmWatch falls back to the neutral embedded resources when these optional translations are absent. The CI package step now fails if any FarmWatch satellite DLL is accidentally included.

CI and Raspberry Pi verification are still required. Validate `GET /api/farmwatch/Status`, `GET /api/farmwatch/Statistics`, diagnostics/events, settings writes, and Connect status before calling the UI functional.


**2.0.0.12 is an investigation build, not a beta or production release.** It keeps the source-generated JSON metadata while switching calls to the long-standing Type-based serializer APIs, and replaces the MVC `NotFound(object)` response with an explicit `ObjectResult` status. This targets the missing-method failures reported on ASF 6.3.11.7 ARM64.

The repository's ASF submodule is pinned to the signed ASF 6.3.11.7 tag commit. CI must build against that exact source pin. Raspberry Pi validation is still required for diagnostics writes, farming callbacks, PICS processing, dashboard settings updates, statistics persistence, and Connect upload/retry/acknowledgement.


**2.0.0.11 is an investigation build, not a beta or production release.** It follows the Raspberry Pi startup fix in 2.0.0.10 and changes FarmWatch's diagnostics and statistics persistence to use one explicitly configured source-generated JSON context. The JSON naming policy remains camelCase for compatibility with existing FarmWatch data files.

Connect outbox loading, persistence and HTTP payload generation now use strongly typed DTOs registered in the shared source-generated JSON context. Receiver acknowledgement parsing also rejects malformed or non-true responses, and automated tests cover the outbox envelope round-trip and acknowledgement rules. A live receiver/HTTP retry test and Raspberry Pi ASF 6.3.11.7 smoke test are still required before Connect or this alpha can be considered validated.

## Development and validation

The repository includes automated tests for Free-to-Keep eligibility, activation policy, promotion evidence, and metadata fixtures. CI runs the test suite and builds the plugin package.

Passing CI confirms the code builds and the automated tests pass; it does not replace testing against a live Steam promotion. The next validation milestones are:

- Complete a real-world genuine Free-to-Keep promotion test in observe-only mode.
- Confirm exclusions for free weekends, demos, playtests, and temporary access.
- Verify settings survive an ASF restart and apply correctly to each bot.
- Publish a versioned release and test ASF-native updating on the Raspberry Pi.
- Only then consider enabling automatic claims, with explicit user approval.

## Project identity and upstream attribution

FarmWatch is an independent continuation and substantial modification of [Citrinate/FreePackages](https://github.com/Citrinate/FreePackages). The upstream project provided the starting point; FarmWatch adds its own branding, strict Free-to-Keep eligibility and evidence gates, safety controls, dashboard, and project direction.

The project retains the applicable **Apache License 2.0** terms and upstream attribution. See [LICENSE](LICENSE).

## Contributing and support

Please use the repository's [Issues](https://github.com/Tjketley/FarmWatch-2/issues) to report reproducible bugs or suggest improvements. Include your ASF version, FarmWatch version, relevant redacted logs, and steps to reproduce. Never post Steam credentials, access tokens, cookies, or other secrets.
