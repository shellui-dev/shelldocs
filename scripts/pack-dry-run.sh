#!/usr/bin/env bash
# Packs every IsPackable=true project into ./nupkgs-dryrun/ and validates each
# .nupkg — verifies README embed, checks size, prints package ID + version.
# Run from repo root.
#
#   ./scripts/pack-dry-run.sh

set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

out_dir="$repo_root/nupkgs-dryrun"
rm -rf "$out_dir"
mkdir -p "$out_dir"

echo
echo "-> dotnet pack shelldocs.slnx -c Release -o $out_dir"
echo
dotnet pack shelldocs.slnx --configuration Release --output "$out_dir"

echo
echo "Produced packages:"
echo

fail=0
for nupkg in "$out_dir"/*.nupkg; do
    [ -e "$nupkg" ] || continue
    size_kb=$(( $(stat -c%s "$nupkg" 2>/dev/null || stat -f%z "$nupkg") / 1024 ))
    echo "  $(basename "$nupkg")  (${size_kb} KB)"

    tmp="$(mktemp -d)"
    unzip -q "$nupkg" -d "$tmp"

    if ! find "$tmp" -name README.md -print -quit | grep -q .; then
        echo "    MISSING README.md" >&2
        fail=$((fail + 1))
    fi

    nuspec="$(find "$tmp" -name '*.nuspec' | head -n 1)"
    if [ -n "$nuspec" ]; then
        id="$(sed -n 's:.*<id>\([^<]*\)</id>.*:\1:p' "$nuspec" | head -n 1)"
        ver="$(sed -n 's:.*<version>\([^<]*\)</version>.*:\1:p' "$nuspec" | head -n 1)"
        echo "    id=$id  version=$ver"
    fi

    rm -rf "$tmp"
done

echo
if [ "$fail" -gt 0 ]; then
    echo "$fail package(s) failed validation" >&2
    exit 1
fi
echo "OK — all packages passed validation"
echo
echo "Ship it with:"
echo "  git tag v<version>"
echo "  git push origin v<version>"
