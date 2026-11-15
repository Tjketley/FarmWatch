#!/usr/bin/env bash
set -euo pipefail

# This is the exact ASF-ui gitlink pinned by the ASF 6.3.11.7 source tag.
ASF_UI_COMMIT="66cde9c899248f2c2f2b4e8d402646a8642cb345"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="${RUNNER_TEMP:-/tmp}/farmwatch-asf-ui"

rm -rf "$WORK"
git clone --filter=blob:none https://github.com/JustArchiNET/ASF-ui.git "$WORK"
git -C "$WORK" checkout --detach "$ASF_UI_COMMIT"

cp "$ROOT/ui/FarmWatchOverview.vue" "$WORK/src/views/FarmWatchOverview.vue"
cp "$ROOT/ui/farmwatch-theme.scss" "$WORK/src/style/farmwatch-theme.scss"

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

routes = root / "src/router/routes.js"
text = routes.read_text()
needle = "  home: 'home',"
assert needle in text, "ASF-ui route anchor changed"
text = text.replace(needle, needle + "\n  farmWatch: 'farmwatch',", 1)
needle = "  {\n    path: '/bots',"
assert needle in text, "ASF-ui bots route anchor changed"
route = """  {
    path: '/farmwatch',
    name: routes.farmWatch,
    component: () => import('../views/FarmWatchOverview.vue'),
  },
"""
text = text.replace(needle, route + needle, 1)
routes.write_text(text)

nav = root / "src/components/App/Navigation.vue"
text = nav.read_text()
needle = '        <NavigationLink :name="$t(\'bots\')" icon="users" :to="{ name: \'bots\' }"></NavigationLink>'
assert needle in text, "ASF-ui navigation anchor changed"
text = text.replace(needle, needle + '\n        <NavigationLink name="FarmWatch" icon="eye" :to="{ name: \'farmwatch\' }"></NavigationLink>', 1)
nav.write_text(text)
PY

cd "$WORK"
npm ci --no-audit --no-fund
npm run build

mkdir -p "$ROOT/FarmWatch/www"
cp -R dist/. "$ROOT/FarmWatch/www/"
# Retain FarmWatch's own mark in the root package.
if [ -f "$ROOT/FarmWatch/www/farmwatch-mark.svg" ]; then
  :
else
  cp "$ROOT/FarmWatch/www/farmwatch-mark.svg" "$ROOT/FarmWatch/www/farmwatch-mark.svg" 2>/dev/null || true
fi
cp "$WORK/LICENSE" "$ROOT/FarmWatch/www/ASF-ui-LICENSE"
printf '%s\n' "$ASF_UI_COMMIT" > "$ROOT/FarmWatch/www/ASF-ui-UPSTREAM-COMMIT"
