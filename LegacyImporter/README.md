# FarmWatch package importer (legacy utility)

This is a legacy userscript retained from the upstream FreePackages project. It transfers package IDs displayed by SteamDB's [Free Packages tool](https://steamdb.info/freepackages/) into an ASF plugin queue through ASF's IPC API.

**Compatibility note:** this utility has not yet been rebranded, integrated into the FarmWatch dashboard, or revalidated against the current FarmWatch build. Treat it as an experimental legacy helper, not a supported FarmWatch feature. It can submit package IDs directly and must not be confused with FarmWatch's verified automatic Free-to-Keep discovery and claim path.

## Installation

1. Install a userscript manager such as [Violentmonkey](https://violentmonkey.github.io/).
2. Review the userscript source in [code.user.js](code.user.js) before installing it.
3. Use it only with a trusted ASF IPC endpoint and an appropriately protected ASF instance.

## Usage

The original userscript adds controls to SteamDB's free packages page to submit selected package IDs to ASF. Its original screenshots are retained for reference:

- [Original interface screenshot](Screenshots/interface.png)
- [Original settings screenshot](Screenshots/settings.png)

The userscript and screenshots originate from the upstream FreePackages project. They are not the FarmWatch dashboard and are not included in the FarmWatch release package.
