FarmWatch release for ArchiSteamFarm 6.3.11.7.

### Highlights

- 
### Compatibility

- ASF version: 6.3.11.7 (automatic plugin updates are offered only for this validated ASF version).
- Tested platforms: CI build environment; Raspberry Pi validation: pending.

### Release assets

This release contains two separate ZIPs with the same FarmWatch version:

- `FarmWatch-<version>.zip` — plugin DLL and setup documentation. This is the asset selected by ASF's plugin updater.
- `FarmWatch-ASF-UI-<version>.zip` — standalone FarmWatch-branded ASF web interface. Install/update this separately.

### Installation / update notes

1. Install the FarmWatch plugin ZIP into its own folder under `plugins/FarmWatch/`.
2. Stop ASF and back up its existing `www` directory.
3. Extract the contents of the matching ASF UI ZIP into ASF's `www` directory, with `index.html` and static asset folders directly inside `www`.
4. Restart ASF and open the normal ASF web interface. FarmWatch is available from the sidebar at `/farmwatch`.

ASF's plugin updater updates the plugin ZIP only. Always install the matching ASF UI ZIP from the same release. Preserve existing ASF configuration and databases.

### Safety

Observe-only remains enabled by default. Automatic claiming remains disabled unless the global and individual bot switches are explicitly enabled. A release does not imply that live promotion validation has been completed.
