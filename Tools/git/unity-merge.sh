#!/bin/bash
# Git merge driver wrapper for Unity's bundled UnityYAMLMerge tool.
# Why this wrapper exists: git invokes merge drivers with extension-less temp
# file paths (e.g. ".git/.merge_file_XXXXXX"), but UnityYAMLMerge picks its
# merge rules from the file extension and silently refuses ("Don't know how
# to merge ... files") when it can't see one. This copies the three inputs
# to properly-named temp files first, then copies the result back.
#
# One-time setup (each teammate runs this once after pulling):
#   git config --local include.path ../.gitconfig-unitymerge
#
# That pulls in the driver registration from .gitconfig-unitymerge (committed
# at the repo root) which points here. .gitattributes (also committed) tells
# git which file types should use it.
#
# Args (from the git driver line in .gitconfig-unitymerge): %O %A %B %P
#   $1 = base (common ancestor)    $2 = ours (also the output path)
#   $3 = theirs                    $4 = the real repo-relative path (for its extension)

BASE="$1"; OURS="$2"; THEIRS="$3"; REALPATH="$4"
EXT="${REALPATH##*.}"

UNITYYAMLMERGE=$(find "/c/Program Files/Unity/Hub/Editor" -maxdepth 5 -iname "UnityYAMLMerge.exe" 2>/dev/null | sort -V | tail -1)
if [ -z "$UNITYYAMLMERGE" ]; then
    echo "[unity-merge] UnityYAMLMerge.exe not found under C:/Program Files/Unity/Hub/Editor — is Unity Hub installed there?" >&2
    exit 1
fi

TMPDIR=$(mktemp -d)
trap 'rm -rf "$TMPDIR"' EXIT
cp "$BASE" "$TMPDIR/base.$EXT"
cp "$OURS" "$TMPDIR/ours.$EXT"
cp "$THEIRS" "$TMPDIR/theirs.$EXT"

"$UNITYYAMLMERGE" merge -p -h --fallback none \
    "$TMPDIR/base.$EXT" "$TMPDIR/ours.$EXT" "$TMPDIR/theirs.$EXT" "$TMPDIR/result.$EXT"
RC=$?

if [ -f "$TMPDIR/result.$EXT" ]; then
    cp "$TMPDIR/result.$EXT" "$OURS"
fi
exit $RC
