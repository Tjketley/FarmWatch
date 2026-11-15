FarmWatch release for ArchiSteamFarm 6.3.11.7.

### Highlights

- 
### Compatibility

- ASF version: 6.3.11.7 (automatic plugin updates are offered only for this validated ASF version).
- Tested platforms: CI build environment; Raspberry Pi validation: pending.

### Single-install package

The release contains one ZIP: `FarmWatch-<version>.zip`. It includes the FarmWatch plugin DLL and the matching FarmWatch-branded ASF web interface under `www/`.

### Installation and updates

1. Extract the FarmWatch ZIP into its own folder under ASF's `plugins` directory (for example, `plugins/FarmWatch/`).
2. Restart ASF.
3. Open ASF at its normal web interface address. ASF serves the bundled FarmWatch UI automatically through the plugin's native `IWebInterface` integration.

ASF's plugin updater downloads the same single ZIP, so the matching UI is updated together with the plugin. No manual copying into ASF's own `www` directory is required. FarmWatch's web assets remain isolated in the plugin folder and do not overwrite ASF files or configuration.

### Safety

Observe-only remains enabled by default. Automatic claiming remains disabled unless the global and individual bot switches are explicitly enabled. A release does not imply that live promotion validation has been completed.
