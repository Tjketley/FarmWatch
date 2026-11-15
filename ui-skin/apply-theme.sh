#!/usr/bin/env bash
set -euo pipefail
ASF_UI_DIR="${1:-}"
THEME_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -z "$ASF_UI_DIR" || ! -f "$ASF_UI_DIR/src/App.vue" ]]; then
  echo "Usage: $0 /path/to/ASF-ui" >&2
  exit 2
fi
TARGET="$ASF_UI_DIR/src/style/farmwatch-theme.scss"
cp "$THEME_DIR/farmwatch-theme.scss" "$TARGET"
if ! grep -q "farmwatch-theme.scss" "$ASF_UI_DIR/src/App.vue"; then
  python3 - "$ASF_UI_DIR/src/App.vue" <<'PY'
from pathlib import Path
import sys
path = Path(sys.argv[1])
text = path.read_text()
needle = "  @import './style/components';"
if needle not in text:
    raise SystemExit("Could not find expected ASF-ui stylesheet import; refusing to modify App.vue")
text = text.replace(needle, needle + "\n  @import './style/farmwatch-theme';", 1)
path.write_text(text)
PY
fi
echo "FarmWatch theme applied to official ASF-ui source."
