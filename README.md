<p align="center"><img src="assets/farmwatch-mark.svg" alt="FarmWatch eye and shield logo" width="150"></p>

# FarmWatch

**Steam Free-to-Keep monitoring and guarded licence claiming for ArchiSteamFarm.**

FarmWatch watches Steam's Product Information and Change Service (PICS) for promotion changes, verifies whether a package is genuinely being offered as a permanent Free-to-Keep licence, and can optionally claim eligible promotions for selected ASF bots.

It is designed around one rule: **discover broadly, verify carefully, claim only when every safety check agrees.**

<p align="center">
  <strong>NEON GREEN · DEEP NAVY · FARMWATCH</strong><br>
  <em>Independent ASF plugin · Raspberry Pi friendly · Open source</em>
</p>

> **Development status:** FarmWatch is under active development. The ASF dashboard and settings API are being built and tested. A public release and end-to-end Raspberry Pi auto-update test have not yet been completed. Keep auto-claim disabled until the real-promotion test milestone is complete.

## What FarmWatch does

- **Promotion monitoring** — listens for Steam PICS changes and evaluates newly changed app and package metadata.
- **Strict Free-to-Keep eligibility** — requires explicit Steam free-promotion evidence, NoCost billing, a SinglePurchase licence, one eligible game, and current availability.
- **Fresh verification before claiming** — fetches package metadata again immediately before activation and requires matching, unexpired evidence no older than five minutes.
- **Observe-only mode** — discovers and verifies eligible candidates without activating licences. This is the default operating mode.
- **Optional per-bot claiming** — global and per-bot switches must all explicitly permit activation.
- **Owned-game free DLC** — handled through a separate evidence path that requires a free DLC classification and a verified parent game already owned by that bot.
- **ASF web dashboard** — view runtime status and manage global safety switches and per-bot auto-claim settings at `/FarmWatch`.
- **Persistent settings** — FarmWatch-owned settings are stored separately in `config/FarmWatch.json`.
- **ASF-native update support** — implements ASF's GitHub plugin update interface so ASF can discover published FarmWatch releases.
- **Existing ASF workflows** — retains compatible legacy FreePackages configuration keys and commands during the transition.

## Safety model

FarmWatch is deliberately fail-closed.

| Control | Default | Purpose |
|---|---:|---|
| Observe-only | **On** | Monitor and verify without claiming |
| Global auto-claim | **Off** | Master permission for automatic claims |
| Per-bot auto-claim | **Off** | Explicit permission for each bot |

A Free-to-Keep activation is permitted only when all of the following are true:

1. Observe-only has explicitly been turned off.
2. The global auto-claim master switch is enabled.
3. The relevant bot's auto-claim switch is enabled.
4. Steam metadata confirms a permanent, no-cost, single-purchase promotion for one eligible game.
5. A fresh metadata recheck and current evidence pass the final activation gate.

**Not eligible for the Free-to-Keep claim path:** free weekends, demos, playtests, temporary access, free-to-play catalogue apps, multi-app bundles, paid packages, and DLC giveaways presented as base-game promotions.

Free DLC for an already-owned game is a separate, narrowly verified path. Its evidence cannot be used to authorise a base-game Free-to-Keep claim.

Turning on a dashboard switch does not bypass these checks. Missing or invalid evidence means no claim.

## Dashboard

The FarmWatch page is served by ASF at:

`/FarmWatch`

It provides a runtime overview, connection status for managed bots, global observe-only and auto-claim controls, and individual bot auto-claim switches. Changes are sent to ASF's authenticated API and saved in FarmWatch's own settings file.

The dashboard is part of the plugin; it does not require a separate web server. Keep ASF's IPC/web interface protected and do not expose it directly to the public internet without appropriate secure access controls.

## Requirements

- ArchiSteamFarm **6.3.11.7** (the current development target; later compatibility will be documented when tested).
- A supported ASF installation with plugin support and its IPC/web interface enabled.
- A Steam bot configured and connected in ASF.
- .NET runtime requirements matching the ASF build you use.

FarmWatch is being developed and tested against ASF-generic on 64-bit ARM Raspberry Pi OS as well as the CI build environment. Other ASF variants should be treated as unverified until tested.

## Installation

**No public release is available yet.** Once the first release is published, installation will be:

1. Download the FarmWatch ZIP from the [GitHub Releases](https://github.com/Tjketley/FarmWatch-2/releases) page.
2. Extract it into its own folder under ASF's `plugins` directory (for example, `plugins/FarmWatch/`).
3. Restart ASF.
4. Confirm the FarmWatch plugin loads in the ASF log.
5. Open the ASF web interface and navigate to `/FarmWatch`.

Do not replace or delete your existing ASF configuration, bot files, or databases during installation. FarmWatch keeps its own settings and per-bot cache separate.

## Updating

FarmWatch implements ASF's `IGitHubPluginUpdates` integration and points to this repository. Once versioned GitHub releases are published with the expected ZIP asset, ASF's plugin update mechanism can discover and install updates according to the ASF update settings.

The release workflow builds and packages the plugin when a version tag is pushed. **Automatic updating is not considered verified until a real update from one published release to a newer release has been tested on the target Raspberry Pi.** Until then, treat updates as a planned capability rather than a completed end-to-end test.

## Configuration and compatibility

The dashboard is the intended user-facing place to manage FarmWatch's safety switches. Legacy FreePackages keys and commands remain in the code for compatibility with existing setups. They are not the preferred way to configure new FarmWatch installations.

FarmWatch uses a separate `config/FarmWatch.json` file for its global and per-bot safety settings. Do not edit this file while ASF is running; use the dashboard so changes are validated, applied, and saved together.

## Privacy

FarmWatch uses Steam/ASF data needed to discover promotions, check package metadata, and verify ownership for the separate free-DLC path. The FarmWatch dashboard is hosted locally by ASF. It does not send Steam credentials, cookies, session identifiers, or account library details to a FarmWatch-operated website. Any future companion website integration is intended to use aggregate statistics only.

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
