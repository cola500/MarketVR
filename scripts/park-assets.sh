#!/usr/bin/env bash
# EPIC-013/014: selective import of a few models from the Unity Asset Store package "Low Poly Environment Park"
# (Palmov Island, free, Standard Unity Asset Store EULA). Only the files listed below are unpacked, each with its own
# .meta (the package's GUIDs): no demo scenes, prefabs, project settings or render pipeline changes come along.
# The imported folder is git-ignored (Asset Store content is not committed); a fresh clone runs this script once.
# Without the files the market still works: SceneSetup leaves the park models out and FruitStand builds no trees.
#
#   scripts/park-assets.sh          # unpack the selection into UnityProject/Assets/Palmov Island/
#   scripts/park-assets.sh list     # show what the package contains (nothing is written)
#
# The package must be in the local Asset Store cache: Unity Package Manager > My Assets > Low Poly Environment Park >
# Download (not Import). Then run `scripts/quest.sh scene` so the scene references the models.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PACKAGE="${PARK_PACKAGE:-$HOME/Library/Unity/Asset Store-5.x/Palmov Island/3D ModelsEnvironments/Low Poly Environment Park.unitypackage}"
BASE="Assets/Palmov Island/Low Poly Environment Park"
SELECTION=(
    "$BASE/Materials/mat main.mat"
    "$BASE/Textures/texture main.png"
    "$BASE/Models/Trees/spherical tree.fbx"
    "$BASE/Models/Trees/tree round foliage.fbx"
    "$BASE/Models/Trees/tree oak.fbx"
    "$BASE/Models/Trees/deciduous tree.fbx"
    "$BASE/Models/Environment/bench 1.fbx"
    "$BASE/Models/Grass/grass 3.fbx"
    "$BASE/Models/Grass/grass 4.fbx"
    "$BASE/Models/Stones/stone 1.fbx"
    "$BASE/Models/Stones/stone 3.fbx"
    "$BASE/Models/Trees/birch tree.fbx"       # EPIC-014: more kinds for the groups round the park
    "$BASE/Models/Trees/elongated tree.fbx"
)

[[ -f "$PACKAGE" ]] || { echo "[park] package not found: $PACKAGE (Package Manager > My Assets > Download)" >&2; exit 2; }
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
tar -xzf "$PACKAGE" -C "$tmp"

if [[ "${1:-}" == "list" ]]; then
    for d in "$tmp"/*/; do [[ -f "$d/asset" ]] && echo "$(head -1 "$d/pathname")"; done | sort
    exit 0
fi

n=0
for d in "$tmp"/*/; do
    path="$(head -1 "$d/pathname")"
    keep=0
    for want in "${SELECTION[@]}"; do
        # the selected files and the folders above them (their .meta keeps the package's folder GUIDs)
        if [[ "$path" == "$want" || "$want" == "$path"/* ]]; then keep=1; break; fi
    done
    (( keep )) || continue
    dest="$ROOT/UnityProject/$path"
    if [[ -f "$d/asset" ]]; then
        mkdir -p "$(dirname "$dest")"
        cp "$d/asset" "$dest"
        n=$((n + 1))
    else
        mkdir -p "$dest"
    fi
    [[ -f "$d/asset.meta" ]] && cp "$d/asset.meta" "$dest.meta"
done
(( n == ${#SELECTION[@]} )) || { echo "[park] expected ${#SELECTION[@]} files, unpacked $n" >&2; exit 3; }
echo "[park] unpacked $n files into UnityProject/$BASE (git-ignored). Next: scripts/quest.sh scene"
