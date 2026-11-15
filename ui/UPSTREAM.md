# ASF-ui upstream pin

FarmWatch currently targets ArchiSteamFarm 6.3.11.7. The ASF 6.3.11.7 source tag (`6dca024487b9cfe47fc8cc86a38d9dcaa17f5cc8`) records ASF-ui as a git submodule at:

`66cde9c899248f2c2f2b4e8d402646a8642cb345`

The unified UI build checks out that exact ASF-ui commit rather than following `main`. This ties the UI source to the version ASF 6.3.11.7 itself shipped with and avoids accidentally consuming later API/schema changes.

Source: https://github.com/JustArchiNET/ASF-ui/tree/66cde9c899248f2c2f2b4e8d402646a8642cb345
ASF source tag: https://github.com/JustArchiNET/ArchiSteamFarm/tree/6.3.11.7

ASF-ui is licensed under Apache License 2.0. The build includes its license in the plugin's `www/ASF-ui-LICENSE` file and records the upstream commit in `www/ASF-ui-UPSTREAM-COMMIT`.

## Build

Run `bash ui/build-unified-ui.sh` from the repository root. The script clones the pinned upstream source, applies the FarmWatch route/navigation/theme overlay, builds the production UI, and places the static output in `FarmWatch/www`.

The plugin web path intentionally remains `/FarmWatch` during this integration phase. Do not change it to `/` until the complete application has passed native ASF feature, authentication, API, packaging and Raspberry Pi runtime tests.
