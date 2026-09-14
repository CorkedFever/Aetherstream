#!/usr/bin/env bash
#
# Cut a release: bump, build, package, verify, tag, publish, and update every manifest mirror.
#
#   ./release.sh 0.2.10 --notes notes.md         # the real thing
#   ./release.sh 0.2.10 --notes notes.md --dry-run   # everything up to the point of no return
#
# Every step here exists because its absence once shipped something wrong:
#   - the build's exit code is checked directly (a grep on its output once masked a failed
#     compile, and a zip of the *previous* binaries went out under a new version number);
#   - the manifest inside the zip is asserted before upload, not assumed;
#   - the zip is never in the working tree when git add runs (one made it into history once);
#   - GitHub Pages is told to build (it silently stopped picking up pushes twice) and the script
#     waits until the fallback mirror actually serves the new version.

set -euo pipefail
cd "$(dirname "$0")"

# -- arguments ---------------------------------------------------------------------------------

VERSION="${1:-}"
NOTES=""
DRY_RUN=0
DEV_COPY=1

shift || true
while [ $# -gt 0 ]; do
    case "$1" in
        --notes)    NOTES="$2"; shift 2 ;;
        --dry-run)  DRY_RUN=1; shift ;;
        --no-dev)   DEV_COPY=0; shift ;;
        *) echo "unknown argument: $1"; exit 2 ;;
    esac
done

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: $0 <major.minor.patch> --notes <file> [--dry-run] [--no-dev]"; exit 2; }
if [ "$DRY_RUN" = 0 ]; then
    [ -n "$NOTES" ] && [ -f "$NOTES" ] || { echo "release notes file required: --notes <file>"; exit 2; }
fi

REPO="CorkedFever/Aetherstream"
PLUGIN="src/Aetherstream.Plugin"
OUT_DIR="$PLUGIN/bin/Debug"
DEV_DIR="$HOME/AppData/Roaming/XIVLauncher/devPlugins/Aetherstream"
TAG="v$VERSION"
ASSEMBLY="$VERSION.0"

say() { printf '\n== %s\n' "$*"; }
fail() { printf '\n!! %s\n' "$*" >&2; exit 1; }

# -- preconditions ------------------------------------------------------------------------------

say "preconditions"
[ "$(git rev-parse --abbrev-ref HEAD)" = "main" ] || fail "release from main, not $(git rev-parse --abbrev-ref HEAD)"
[ -z "$(git status --porcelain)" ] || fail "working tree is not clean — commit or stash first"
git fetch -q origin main
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || fail "main is not in sync with origin — push or pull first"
git rev-parse -q --verify "refs/tags/$TAG" >/dev/null && fail "tag $TAG already exists"
command -v gh >/dev/null || fail "gh is required"
command -v dotnet >/dev/null || fail "dotnet is required"
command -v python >/dev/null || fail "python is required"
gh auth status >/dev/null 2>&1 || fail "gh is not signed in"

PREVIOUS="$(python - <<'PY'
import json
print(json.load(open('repo.json', encoding='utf-8'))[0]['AssemblyVersion'])
PY
)"
echo "   $PREVIOUS -> $ASSEMBLY"

# -- bump ----------------------------------------------------------------------------------------

say "bumping to $ASSEMBLY"
BUMPED=("$PLUGIN/Aetherstream.Plugin.csproj" "$PLUGIN/Aetherstream.json" "repo.json" "docs/repo.json")
python - "$ASSEMBLY" "$TAG" <<'PY'
import io, json, re, sys, time
assembly, tag = sys.argv[1], sys.argv[2]

p = 'src/Aetherstream.Plugin/Aetherstream.Plugin.csproj'
s = io.open(p, encoding='utf-8').read()
s, n = re.subn(r'<Version>[0-9.]+</Version>', f'<Version>{assembly}</Version>', s, count=1)
assert n == 1, 'no <Version> in csproj'
io.open(p, 'w', encoding='utf-8').write(s)

p = 'src/Aetherstream.Plugin/Aetherstream.json'
m = json.load(io.open(p, encoding='utf-8')); m['AssemblyVersion'] = assembly
io.open(p, 'w', encoding='utf-8').write(json.dumps(m, indent=2) + "\n")

for p in ('repo.json', 'docs/repo.json'):
    r = json.load(io.open(p, encoding='utf-8')); e = r[0]
    e['AssemblyVersion'] = e['TestingAssemblyVersion'] = assembly
    e['LastUpdate'] = int(time.time())
    for k in ('DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting'):
        e[k] = re.sub(r'/download/v[0-9.]+/', f'/download/{tag}/', e[k])
    io.open(p, 'w', encoding='utf-8').write(json.dumps(r, indent=2) + "\n")
print('   csproj, plugin manifest, repo.json, docs/repo.json')
PY

# -- build ---------------------------------------------------------------------------------------

say "building"
# The exit code, directly. Never through a pipe: `dotnet build | grep` reports grep's status.
if ! dotnet build "$PLUGIN/Aetherstream.Plugin.csproj" -v q --nologo > /tmp/aetherstream-build.log 2>&1; then
    grep -E "error" /tmp/aetherstream-build.log | sort -u >&2
    git checkout -- "${BUMPED[@]}"
    fail "build failed — version bumps reverted"
fi
grep -q "Build succeeded" /tmp/aetherstream-build.log || { git checkout -- "${BUMPED[@]}"; fail "no 'Build succeeded' in the build log"; }
STALE="$(find src -name '*.cs' -newer "$OUT_DIR/Aetherstream.dll" | head -3)"
[ -z "$STALE" ] || { git checkout -- "${BUMPED[@]}"; fail "sources newer than the built DLL: $STALE"; }
echo "   ok"

# -- package -------------------------------------------------------------------------------------

say "packaging"
# Outside the tree (never near git add), and named exactly Aetherstream.zip: the asset takes the
# file's name, and the manifests link to that name. A temp-suffixed file once shipped as a 404.
ZIP_DIR="$(mktemp -d)"
ZIP="$ZIP_DIR/Aetherstream.zip"
python - "$OUT_DIR" "$ZIP" "$ASSEMBLY" <<'PY'
import os, sys, zipfile
root, out, assembly = sys.argv[1], sys.argv[2], sys.argv[3]
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for d, _, fs in os.walk(root):
        for f in fs:
            full = os.path.join(d, f); z.write(full, os.path.relpath(full, root))
with zipfile.ZipFile(out) as z:
    names = z.namelist()
    manifest = z.read('Aetherstream.json').decode()
    # The zip is what people install. Its own manifest is the truth, not the working tree.
    assert f'"AssemblyVersion": "{assembly}"' in manifest, 'zip manifest has the wrong version'
    assert '"Author": "CorkedFever"' in manifest, 'zip manifest has the wrong author'
    for required in ('Aetherstream.dll', 'Aetherstream.json', 'SixLabors.ImageSharp.dll', 'images/icon.png', 'images/testcard.rgba.gz', 'images/guidefont.a8.gz', 'data/fish.json.gz', 'Fonts/VT323-Regular.ttf', 'libvlc/win-x64/libvlc.dll'):
        assert required in names, f'zip is missing {required}'
print(f'   {out} ({os.path.getsize(out) / 1e6:.1f} MB), manifest {assembly}, author and icon verified')
PY

# Not on a dry run: the dev folder would otherwise carry a version number nothing was released as.
if [ "$DRY_RUN" = 0 ] && [ "$DEV_COPY" = 1 ] && [ -d "$DEV_DIR" ]; then
    say "dev copy"
    for f in Aetherstream.dll Aetherstream.json Aetherstream.Core.dll Aetherstream.Playback.dll SixLabors.ImageSharp.dll; do cp -f "$OUT_DIR/$f" "$DEV_DIR/$f"; done
    mkdir -p "$DEV_DIR/images" && for f in icon.png testcard.rgba.gz guidefont.a8.gz; do cp -f "$OUT_DIR/images/$f" "$DEV_DIR/images/$f"; done
    mkdir -p "$DEV_DIR/data" && cp -f "$OUT_DIR/data"/*.gz "$DEV_DIR/data/"
    [ -d "$OUT_DIR/music" ] && mkdir -p "$DEV_DIR/music" && cp -f "$OUT_DIR/music"/* "$DEV_DIR/music/"
    echo "   $DEV_DIR"
fi

if [ "$DRY_RUN" = 1 ]; then
    say "dry run — reverting the bumps, keeping the zip for inspection"
    git checkout -- "${BUMPED[@]}"
    echo "   $ZIP"
    exit 0
fi

# -- point of no return: commit, tag, push, publish ----------------------------------------------

say "committing and tagging $TAG"
git add "${BUMPED[@]}"
git -c core.safecrlf=false commit -q -m "Release $TAG

Co-Authored-By: Claude <noreply@anthropic.com>"
git tag -a "$TAG" -m "Aetherstream $TAG"
git push -q origin main --tags
echo "   $(git rev-parse --short HEAD)"

say "publishing the release"
gh release create "$TAG" "$ZIP" --title "Aetherstream $TAG" --notes-file "$NOTES" >/dev/null
gh release view "$TAG" --json assets -q '.assets[] | "   \(.name) \(.size) bytes"'
curl -sIL "https://github.com/$REPO/releases/download/$TAG/Aetherstream.zip" | grep -qE '^HTTP/2 200' || fail "the release asset does not answer 200 — the zip is still at $ZIP"
rm -rf "$ZIP_DIR"

# -- mirrors -------------------------------------------------------------------------------------

say "mirrors"
if [ -f "$HOME/.ssh/luna" ]; then
    scp -q -i "$HOME/.ssh/luna" -o BatchMode=yes repo.json root@165.227.89.5:/opt/tsukino/site/aetherstream-repo.json \
        && echo "   luna: updated" || echo "   luna: unreachable (skipped)"
else
    echo "   luna: no key here (skipped)"
fi

# Pages does not reliably build on its own after a run of pushes. Ask, then wait for proof.
gh api -X POST "repos/$REPO/pages/builds" -q '"   pages: build \(.status)"'
for _ in $(seq 1 60); do
    if curl -s "https://corkedfever.github.io/Aetherstream/repo.json" | grep -q "\"$ASSEMBLY\""; then
        echo "   pages: serving $ASSEMBLY"; break
    fi
    sleep 10
done
curl -s "https://corkedfever.github.io/Aetherstream/repo.json" | grep -q "\"$ASSEMBLY\"" || echo "   pages: still not serving $ASSEMBLY after 10 minutes — check the build"

say "released $TAG"
