#!/usr/bin/env bash
set -euo pipefail

# This is the exact ASF-ui gitlink pinned by the ASF 6.3.11.7 source tag.
ASF_UI_COMMIT="66cde9c899248f2c2f2b4e8d402646a8642cb345"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="${RUNNER_TEMP:-/tmp}/farmwatch-asf-ui"

rm -rf "$WORK"
MARK_TMP="$ROOT/FarmWatch/farmwatch-mark.svg"
cp "$ROOT/FarmWatch/www/farmwatch-mark.svg" "$MARK_TMP"
git clone --filter=blob:none https://github.com/JustArchiNET/ASF-ui.git "$WORK"
git -C "$WORK" checkout --detach "$ASF_UI_COMMIT"

cp "$ROOT/ui/FarmWatchOverview.vue" "$WORK/src/views/FarmWatchOverview.vue"
cp "$ROOT/ui/FarmWatchAnalytics.vue" "$WORK/src/views/FarmWatchAnalytics.vue"
cp "$ROOT/ui/FarmWatchSafety.vue" "$WORK/src/views/FarmWatchSafety.vue"
cp "$ROOT/ui/farmwatch-theme.scss" "$WORK/src/style/farmwatch-theme.scss"
cp "$MARK_TMP" "$WORK/src/static/farmwatch-mark.svg"

python3 - "$WORK" <<'PY'
from pathlib import Path
import sys
root = Path(sys.argv[1])

app = root / "src/App.vue"
text = app.read_text()
needle = "  @import './style/components';"
assert needle in text, "ASF-ui App.vue style import anchor changed"
text = text.replace(needle, needle + "\n  @import './style/farmwatch-theme';", 1)
app.write_text(text)

# Rebrand browser metadata while retaining ASF's application internals.
text = app.read_text().replace("title: 'ArchiSteamFarm'", "title: 'FarmWatch'").replace("titleTemplate: 'ASF | %s'", "titleTemplate: 'FarmWatch | %s'")
app.write_text(text)

html = root / "src/index.html"
text = html.read_text().replace("<title>ASF</title>", "<title>FarmWatch</title>").replace('href="/images/logo.png"', 'href="/farmwatch-mark.svg"')
html.write_text(text)

routes = root / "src/router/routes.js"
text = routes.read_text()
needle = "  home: 'home',"
assert needle in text, "ASF-ui route anchor changed"
text = text.replace(needle, needle + "\n  farmWatch: 'farmwatch',
  farmWatchAnalytics: 'farmwatch-analytics',
  farmWatchSafety: 'farmwatch-safety',", 1)
needle = "  {\n    path: '/bots',"
assert needle in text, "ASF-ui bots route anchor changed"
route = """  {
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
"""
text = text.replace(needle, route + needle, 1)
routes.write_text(text)

nav = root / "src/components/App/Navigation.vue"
text = nav.read_text()
needle = '        <NavigationLink :name="$t(\'bots\')" icon="users" :to="{ name: \'bots\' }"></NavigationLink>'
assert needle in text, "ASF-ui navigation anchor changed"
text = text.replace(needle, needle + '\n        <NavigationLink name="FarmWatch" icon="eye" :to="{ name: \'farmwatch\' }"></NavigationLink>', 1)
        <NavigationLink name="Analytics" icon="chart-line" :to="{ name: 'farmwatch-analytics' }"></NavigationLink>
        <NavigationLink name="Safety & controls" icon="shield-alt" :to="{ name: 'farmwatch-safety' }"></NavigationLink>
nav.write_text(text)

brand = root / "src/components/Navigation/Brand.vue"
text = brand.read_text().replace('<b>A</b>SF', '<b>F</b>W').replace('<b>Archi</b>SteamFarm', '<b>Farm</b>Watch')
brand.write_text(text)
PY

cd "$WORK"
npm ci --no-audit --no-fund
npm run build

rm -rf "$ROOT/FarmWatch/www"
mkdir -p "$ROOT/FarmWatch/www"
cp -R dist/. "$ROOT/FarmWatch/www/"
# Retain FarmWatch's own mark alongside the ASF-ui static assets.
cp "$MARK_TMP" "$ROOT/FarmWatch/www/farmwatch-mark.svg"
rm -f "$MARK_TMP"
cp "$WORK/LICENSE" "$ROOT/FarmWatch/www/ASF-ui-LICENSE"
printf '%s\n' "$ASF_UI_COMMIT" > "$ROOT/FarmWatch/www/ASF-ui-UPSTREAM-COMMIT"
