#!/usr/bin/env bash
set -euo pipefail

# ASF 6.3.11.7 pins this exact ASF-ui revision.
ASF_UI_COMMIT="66cde9c899248f2c2f2b4e8d402646a8642cb345"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="${RUNNER_TEMP:-/tmp}/farmwatch-asf-ui"
OUTPUT="${1:-$ROOT/out/asf-ui}"

rm -rf "$WORK" "$OUTPUT"
mkdir -p "$(dirname "$OUTPUT")"
git clone --filter=blob:none https://github.com/JustArchiNET/ASF-ui.git "$WORK"
git -C "$WORK" checkout --detach "$ASF_UI_COMMIT"

cp "$ROOT/ui/FarmWatchOverview.vue" "$WORK/src/views/FarmWatchOverview.vue"
cp "$ROOT/ui/FarmWatchAnalytics.vue" "$WORK/src/views/FarmWatchAnalytics.vue"
cp "$ROOT/ui/FarmWatchSafety.vue" "$WORK/src/views/FarmWatchSafety.vue"
cp "$ROOT/ui/FarmWatchDiagnostics.vue" "$WORK/src/views/FarmWatchDiagnostics.vue"
cp "$ROOT/ui/farmwatch-theme.scss" "$WORK/src/style/farmwatch-theme.scss"
cp "$ROOT/FarmWatch/www/farmwatch-mark.svg" "$WORK/src/static/farmwatch-mark.svg"

python3 - "$WORK" <<'PY'
from pathlib import Path
import json
import sys

root = Path(sys.argv[1])

def replace_once(path, old, new, description):
    text = path.read_text()
    if old not in text:
        raise SystemExit(f"ASF-ui upstream anchor not found: {description}")
    path.write_text(text.replace(old, new, 1))

app = root / "src/App.vue"
replace_once(app, "</style>",
             "  @import './style/farmwatch-theme';\n</style>",
             "App.vue final theme import")
text = app.read_text()
text = text.replace("title: 'ArchiSteamFarm'", "title: 'FarmWatch'")
text = text.replace("titleTemplate: 'ASF | %s'", "titleTemplate: 'FarmWatch | %s'")
app.write_text(text)

html = root / "src/index.html"
text = html.read_text()
text = text.replace("<title>ASF</title>", "<title>FarmWatch</title>")
text = text.replace('href="/images/logo.png"', 'href="/farmwatch-mark.svg"')
text = text.replace('${__BASE_PATH__}images/logo.png', '${__BASE_PATH__}farmwatch-mark.svg')
text = text.replace('<meta charset="utf-8">', '<meta charset="utf-8">\n    <meta name="description" content="FarmWatch — premium monitoring and safety controls for ArchiSteamFarm.">\n    <meta name="theme-color" content="#0c141b">')
text = text.replace('  <link href="https://fonts.googleapis.com/css?family=Source+Sans+Pro:300,400,600" rel="stylesheet">\n', '')
html.write_text(text)

manifest = root / "src/static/manifest.json"
manifest_data = json.loads(manifest.read_text())
manifest_data["name"] = "FarmWatch"
manifest_data["short_name"] = "FarmWatch"
manifest_data["description"] = "FarmWatch web interface for ArchiSteamFarm"
manifest_data["icons"] = [{"src": "farmwatch-mark.svg", "sizes": "any", "type": "image/svg+xml"}]
manifest_data["background_color"] = "#0c141b"
manifest.write_text(json.dumps(manifest_data, indent=2) + "\n")

settings = root / "src/style/settings.scss"
settings_text = settings.read_text()
settings_text = settings_text.replace("$color-status-online: #57cbde;", "$color-status-online: #63e6a4;")
settings_text = settings_text.replace("$color-status-farming: #90ba3c;", "$color-status-farming: #b4d9c3;")
settings_text = settings_text.replace("$color-level-info: $color-theme-teal;", "$color-level-info: #63e6a4;")
settings.write_text(settings_text)

routes = root / "src/router/routes.js"
text = routes.read_text()
route_names_anchor = "  home: 'home',"
if route_names_anchor not in text:
    raise SystemExit("ASF-ui upstream anchor not found: route names")
text = text.replace(route_names_anchor, route_names_anchor + """
  farmWatch: 'farmwatch',
  farmWatchAnalytics: 'farmwatch-analytics',
  farmWatchSafety: 'farmwatch-safety',
  farmWatchDiagnostics: 'farmwatch-diagnostics',""", 1)
route_anchor = "  {\n    path: '/bots',"
if route_anchor not in text:
    raise SystemExit("ASF-ui upstream anchor not found: bot route")
route_block = """  {
    path: '/farmwatch',
    name: routes.farmWatch,
    component: () => import('../views/FarmWatchOverview.vue'),
  },
  {
    path: '/farmwatch/analytics',
    name: routes.farmWatchAnalytics,
    component: () => import('../views/FarmWatchAnalytics.vue'),
  },
  {
    path: '/farmwatch/safety',
    name: routes.farmWatchSafety,
    component: () => import('../views/FarmWatchSafety.vue'),
  },
  {
    path: '/farmwatch/diagnostics',
    name: routes.farmWatchDiagnostics,
    component: () => import('../views/FarmWatchDiagnostics.vue'),
  },
"""
routes.write_text(text.replace(route_anchor, route_block + route_anchor, 1))

nav = root / "src/components/App/Navigation.vue"
text = nav.read_text()
nav_anchor = """        <NavigationLink :name="$t('bots')" icon="users" :to="{ name: 'bots' }"></NavigationLink>"""
if nav_anchor not in text:
    raise SystemExit("ASF-ui upstream anchor not found: navigation bot link")
nav_addition = """
        <NavigationLink name="FarmWatch" icon="eye" :to="{ name: 'farmwatch' }"></NavigationLink>
        <NavigationLink name="Analytics" icon="tachometer-alt" :to="{ name: 'farmwatch-analytics' }"></NavigationLink>
        <NavigationLink name="Safety & controls" icon="lock" :to="{ name: 'farmwatch-safety' }"></NavigationLink>
        <NavigationLink name="Diagnostics" icon="clipboard" :to="{ name: 'farmwatch-diagnostics' }"></NavigationLink>"""
nav.write_text(text.replace(nav_anchor, nav_anchor + nav_addition, 1))

brand = root / "src/components/Navigation/Brand.vue"
text = brand.read_text()
text = text.replace('<b>A</b>SF', '<b>F</b>W')
text = text.replace('<b>Archi</b>SteamFarm', '<b>Farm</b>Watch')
brand.write_text(text)
PY

cd "$WORK"
npm ci --no-audit --no-fund
npm run build

mkdir -p "$OUTPUT"
cp -R dist/. "$OUTPUT/"
cp "$WORK/LICENSE" "$OUTPUT/ASF-ui-LICENSE"
printf '%s\n' "$ASF_UI_COMMIT" > "$OUTPUT/ASF-ui-UPSTREAM-COMMIT"
test -f "$OUTPUT/index.html"
test -f "$OUTPUT/farmwatch-mark.svg"
cat > "$OUTPUT/INSTALL.md" <<'EOF'
# FarmWatch ASF UI

This is the standalone FarmWatch-branded ASF web interface built from the ASF-ui revision pinned to ASF 6.3.11.7.

1. Stop ASF before replacing its web assets.
2. Back up the existing ASF `www` directory.
3. Extract the contents of this ZIP into ASF's `www` directory so that `index.html`, `scripts/`, `styles/`, `images/`, and `manifest.json` sit directly inside `www`.
4. Start ASF and open the normal ASF web interface URL.
5. Keep the FarmWatch plugin ZIP installed separately in ASF's plugin directory.

This archive does not contain the FarmWatch plugin DLL. The plugin and UI ZIPs must be installed together from the same release version. ASF's native Bots, Commands, Releases, Plugins, Logs, configuration, authentication, restart and shutdown functions remain part of the upstream UI.
EOF
