#!/bin/bash
set -euo pipefail

PATH=/bin:/sbin:/usr/bin:/usr/sbin:/usr/local/bin:/usr/local/sbin:$HOME/bin
export PATH

_PROGN_=$(basename "$0")
_INSTDIR_=$(cd "$(dirname "$0")" && pwd)
plugin_name=$(basename "$_INSTDIR_")

if [[ $# -gt 1 ]]; then
   echo "Usage: $0 [ASF-tag-or-commit]"
   exit 1
fi

# The committed gitlink is the supported/default ASF version.
# Never silently move the dependency to a newer upstream tag.
git submodule update --init --recursive

if [[ $# -eq 1 ]]; then
   git -C ArchiSteamFarm fetch origin --tags
   git -C ArchiSteamFarm checkout "$1"
fi

echo "Building against pinned ASF dependency:"
git -C ArchiSteamFarm describe --tags --always

rm -rf ./out

dotnet restore
sync
dotnet publish FarmWatch -c "Release" -o "out/generic" "/p:LinkDuringPublish=false"
mkdir -p "./out/$plugin_name"
cp "./out/generic/$plugin_name.dll" "./out/$plugin_name"
( cd ./out/generic ; cp --parents ./*/"$plugin_name".resources.dll ../../out/"$plugin_name" || : )
if [[ -f "README.md" ]]; then
   if ! command -v pandoc &> /dev/null; then
      cp README.md "./out/$plugin_name"
   else
      pandoc --metadata title="$plugin_name" --standalone --columns 2000 -f markdown -t html --embed-resources --standalone -c ./github-pandoc.css -o "./out/$plugin_name/README.html" README.md
   fi
fi
7z a -tzip -mx7 "./out/$plugin_name.zip" "./out/$plugin_name"
rm -rf "./out/$plugin_name"
