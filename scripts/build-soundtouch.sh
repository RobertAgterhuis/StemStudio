#!/usr/bin/env bash
# Builds soundtouch.wasm (SoundTouch 2.4.1, LGPL-2.1, + the StemStudio shim; feature 012, ADR-021)
# exactly as pinned in third_party/soundtouch/soundtouch.lock:
#   1. download SOUNDTOUCH_URL into third_party/soundtouch/upstream/ (skipped when already there)
#      and verify SOUNDTOUCH_SHA256 (a mismatching file is deleted, the build stops);
#   2. compile with em++ (not emcc: C++ link) using EMCC_FLAGS over SOUNDTOUCH_SOURCES and
#      shim/stemstudio_soundtouch.cpp, inside EMSDK_IMAGE@EMSDK_DIGEST with --network none, adding
#      -ffile-prefix-map for the source and shim directories (no build path in the module);
#   3. check the module: no imports, every ABI export present, st_abi_version() == ABI_VERSION;
#   4. write OUTPUT (git-ignored) and print its SHA-256.
#
# Usage: scripts/build-soundtouch.sh [options]
#   (no option)            fetch + verify on the host, compile in the pinned emsdk image (Docker)
#   --local                compile with the em++ on PATH (a local Emscripten SDK of the version in
#                          the lock, or inside the emsdk image); fetches first when the archive is
#                          missing. This is the non-Docker path.
#   --verify-reproducible  Docker build twice in fresh containers; fails unless both SHA-256 match
#   --fetch-only           download + verify the source archive only
#   --package DIR          write the LGPL-2.1 distribution folder (licence, notice, exact source
#                          archive, shim, header, lock, build scripts, SHA256SUMS, index.html) to
#                          DIR; the web image serves it at /third-party/soundtouch/
#   --check-pins           fail unless docker/web.Dockerfile pins the lock's EMSDK_IMAGE@EMSDK_DIGEST
#   --print-config         print the parsed lock values and exit
#   --lock FILE            lock file (default third_party/soundtouch/soundtouch.lock); upstream/,
#                          build/, shim/ and include/ are taken from the lock's directory
#   --shim-dir DIR         directory with stemstudio_soundtouch.cpp (default <lock dir>/shim)
#   --out FILE             output file (default: OUTPUT of the lock, relative to the repo root)
#   -h, --help             this help
# Exit codes: 0 success, 1 failure, 2 usage error.
# Requirements: bash, curl, sha256sum (or shasum), tar; Docker, or for --local em++ and node.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCK="$REPO_ROOT/third_party/soundtouch/soundtouch.lock"
SHIM_DIR=""
OUT=""
MODE=docker
PACKAGE_DIR=""
REQUIRED_KEYS=(SOUNDTOUCH_VERSION SOUNDTOUCH_URL SOUNDTOUCH_SHA256 SOUNDTOUCH_ARCHIVE_NAME
  SOUNDTOUCH_ARCHIVE_ROOT EMSDK_IMAGE EMSDK_DIGEST SOUNDTOUCH_SOURCES EMCC_FLAGS OUTPUT ABI_VERSION)
SHIM_FILE=stemstudio_soundtouch.cpp
ABI_EXPORTS="memory _initialize st_abi_version st_create st_destroy st_set_pitch st_set_setting st_initial_latency st_put st_receive st_clear st_malloc st_free"

die() { echo "build-soundtouch: $*" >&2; exit 1; }
usage() { sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d; s/^# \{0,1\}//'; }

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --local | --verify-reproducible | --fetch-only | --check-pins | --print-config) MODE="${1#--}" ;;
      --package) MODE=package; PACKAGE_DIR="${2:?--package needs a directory}"; shift ;;
      --lock) LOCK="${2:?--lock needs a file}"; shift ;;
      --shim-dir) SHIM_DIR="${2:?--shim-dir needs a directory}"; shift ;;
      --out) OUT="${2:?--out needs a file}"; shift ;;
      -h | --help) usage; exit 0 ;;
      *) echo "build-soundtouch: unknown option: $1 (see --help)" >&2; exit 2 ;;
    esac
    shift
  done
}

# Value of a lock key (bash 3.2 compatible: no associative arrays).
cfg() { local name="LOCK_$1"; printf '%s' "${!name:-}"; }

# Plain KEY=value lines; '#' starts a comment line; no quotes, no expansion (lock contract).
load_lock() {
  [[ -f "$LOCK" ]] || die "lock file not found: $LOCK"
  local line key
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ -z "$line" || "$line" == \#* ]] && continue
    key="${line%%=*}"
    [[ "$key" =~ ^[A-Z][A-Z0-9_]*$ ]] || die "invalid lock line: $line"
    printf -v "LOCK_$key" '%s' "${line#*=}"
  done < "$LOCK"
  for key in "${REQUIRED_KEYS[@]}"; do
    [[ -n "$(cfg "$key")" ]] || die "lock key missing or empty: $key ($LOCK)"
  done
}

resolve_paths() {
  LOCK_DIR="$(cd "$(dirname "$LOCK")" && pwd)"
  LOCK="$LOCK_DIR/$(basename "$LOCK")"
  SHIM_DIR="${SHIM_DIR:-$LOCK_DIR/shim}"
  OUT="${OUT:-$REPO_ROOT/$(cfg OUTPUT)}"
  ARCHIVE="$LOCK_DIR/upstream/$(cfg SOUNDTOUCH_ARCHIVE_NAME)"
  BUILD_DIR="$LOCK_DIR/build"
}

print_config() {
  local key
  for key in "${REQUIRED_KEYS[@]}"; do echo "$key=$(cfg "$key")"; done
  echo "ARCHIVE=$ARCHIVE"
  echo "SHIM_DIR=$SHIM_DIR"
  echo "OUT=$OUT"
}

sha256_of() {
  if command -v sha256sum > /dev/null; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

verify_archive() {
  local actual
  actual="$(sha256_of "$ARCHIVE")"
  if [[ "$actual" != "$(cfg SOUNDTOUCH_SHA256)" ]]; then
    rm -f "$ARCHIVE"
    die "SHA-256 mismatch for $(cfg SOUNDTOUCH_ARCHIVE_NAME): expected $(cfg SOUNDTOUCH_SHA256), got $actual (file deleted)"
  fi
  echo "verified $(cfg SOUNDTOUCH_ARCHIVE_NAME) sha256=$actual"
}

fetch_archive() {
  if [[ ! -f "$ARCHIVE" ]]; then
    mkdir -p "$(dirname "$ARCHIVE")"
    echo "downloading $(cfg SOUNDTOUCH_URL)"
    curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 --retry 3 \
      --output "$ARCHIVE.part" "$(cfg SOUNDTOUCH_URL)" || { rm -f "$ARCHIVE.part"; die "download failed: $(cfg SOUNDTOUCH_URL)"; }
    mv "$ARCHIVE.part" "$ARCHIVE"
  fi
  verify_archive
}

require_shim() {
  [[ -f "$SHIM_DIR/$SHIM_FILE" ]] || die "shim not found: $SHIM_DIR/$SHIM_FILE (audio-engine, 012 T020)"
}

# Fresh extraction for every build: no stale or locally edited upstream file is compiled.
extract_sources() {
  rm -rf "$BUILD_DIR/src"
  mkdir -p "$BUILD_DIR/src"
  tar -xzf "$ARCHIVE" -C "$BUILD_DIR/src" --no-same-owner
  SRC_ROOT="$BUILD_DIR/src/$(cfg SOUNDTOUCH_ARCHIVE_ROOT)"
  [[ -d "$SRC_ROOT/source/SoundTouch" ]] || die "unexpected archive layout: $SRC_ROOT/source/SoundTouch missing"
}

compile_module() {
  local src_dir="$SRC_ROOT/source/SoundTouch" file
  local -a flags sources=()
  read -r -a flags <<< "$(cfg EMCC_FLAGS)"
  for file in $(cfg SOUNDTOUCH_SOURCES); do sources+=("$src_dir/$file"); done
  mkdir -p "$BUILD_DIR/out"
  echo "em++ $(em++ --version | head -n 1)"
  em++ "${flags[@]}" "-ffile-prefix-map=$src_dir=." "-ffile-prefix-map=$SHIM_DIR=." \
    -I "$SRC_ROOT/include" -I "$LOCK_DIR/include" "${sources[@]}" "$SHIM_DIR/$SHIM_FILE" \
    -o "$BUILD_DIR/out/soundtouch.wasm"
}

# Instantiates the module without imports and checks the ABI (node ships with emsdk).
check_module() {
  command -v node > /dev/null || { echo "build-soundtouch: node not found, module check skipped" >&2; return 0; }
  node -e '
    const [file, abi, names] = process.argv.slice(1);
    const mod = new WebAssembly.Module(require("fs").readFileSync(file));
    const imports = WebAssembly.Module.imports(mod);
    if (imports.length) throw new Error("module has imports: " + JSON.stringify(imports));
    const exp = new WebAssembly.Instance(mod, {}).exports;
    const missing = names.split(" ").filter((n) => !(n in exp));
    if (missing.length) throw new Error("missing exports: " + missing.join(", "));
    exp._initialize();
    if (exp.st_abi_version() !== Number(abi)) throw new Error("st_abi_version() != " + abi);
    console.log("module ok: no imports, ABI " + abi + ", " + names.split(" ").length + " exports");
  ' "$1" "$(cfg ABI_VERSION)" "$ABI_EXPORTS"
}

install_output() {
  mkdir -p "$(dirname "$OUT")"
  cp "$BUILD_DIR/out/soundtouch.wasm" "$OUT"
  echo "wrote $OUT ($(wc -c < "$OUT" | tr -d ' ') bytes) sha256=$(sha256_of "$OUT")"
}

build_local() {
  require_shim
  fetch_archive
  extract_sources
  compile_module
  check_module "$BUILD_DIR/out/soundtouch.wasm"
  install_output
}

# Path as Docker expects it for a bind mount (Git Bash on Windows: C:/...).
host_path() { if command -v cygpath > /dev/null; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

# Runs this script with --local inside the pinned image, offline; $1 = output path inside /work.
docker_build() {
  local -a args=(run --rm --network none -v "$(host_path "$REPO_ROOT"):/work" -w /work)
  local shim="$SHIM_DIR" lock_rel="${LOCK#"$REPO_ROOT"/}"
  [[ "$lock_rel" != "$LOCK" ]] || die "Docker mode needs the lock inside the repository: $LOCK"
  if [[ "$SHIM_DIR" != "$REPO_ROOT"/* ]]; then
    args+=(-v "$(host_path "$SHIM_DIR"):/shim:ro"); shim=/shim
  else
    shim="/work/${SHIM_DIR#"$REPO_ROOT"/}"
  fi
  [[ "$(uname -s)" == Linux || "$(uname -s)" == Darwin ]] && args+=(--user "$(id -u):$(id -g)")
  MSYS_NO_PATHCONV=1 docker "${args[@]}" "$(cfg EMSDK_IMAGE)@$(cfg EMSDK_DIGEST)" \
    bash scripts/build-soundtouch.sh --local --lock "/work/$lock_rel" --shim-dir "$shim" --out "/work/$1"
}

build_docker() {
  require_shim
  fetch_archive
  local rel="${OUT#"$REPO_ROOT"/}"
  if [[ "$rel" == "$OUT" ]]; then
    docker_build "$(cfg OUTPUT)"
    mkdir -p "$(dirname "$OUT")"; cp "$REPO_ROOT/$(cfg OUTPUT)" "$OUT"
  else
    docker_build "$rel"
  fi
}

verify_reproducible() {
  require_shim
  fetch_archive
  local rel="${BUILD_DIR#"$REPO_ROOT"/}" first second
  docker_build "$rel/repro/first.wasm"
  docker_build "$rel/repro/second.wasm"
  first="$(sha256_of "$BUILD_DIR/repro/first.wasm")"
  second="$(sha256_of "$BUILD_DIR/repro/second.wasm")"
  [[ "$first" == "$second" ]] || die "not reproducible: first build $first, second build $second"
  mkdir -p "$(dirname "$OUT")"
  cp "$BUILD_DIR/repro/first.wasm" "$OUT"
  echo "reproducible: both builds sha256=$first; wrote $OUT"
}

check_pins() {
  local dockerfile="$REPO_ROOT/docker/web.Dockerfile" pin
  pin="$(cfg EMSDK_IMAGE)@$(cfg EMSDK_DIGEST)"
  grep -q "^ARG EMSDK_IMAGE=$pin\$" "$dockerfile" ||
    die "docker/web.Dockerfile must pin 'ARG EMSDK_IMAGE=$pin' (soundtouch.lock)"
  echo "pins ok: docker/web.Dockerfile uses $pin"
}

package_files() {
  local dir="$1"
  mkdir -p "$dir/shim" "$dir/include" "$dir/scripts"
  cp "$LOCK_DIR/COPYING.TXT" "$LOCK_DIR/NOTICE.md" "$LOCK_DIR/README.md" "$LOCK" "$ARCHIVE" "$dir/"
  cp "$LOCK_DIR"/include/*.h "$dir/include/"
  cp -R "$SHIM_DIR"/. "$dir/shim/"
  cp "$REPO_ROOT/scripts/build-soundtouch.sh" "$REPO_ROOT/scripts/build-soundtouch.ps1" "$dir/scripts/"
}

package_index() {
  local dir="$1" file
  (cd "$dir" && find . -type f ! -name SHA256SUMS ! -name index.html | sed 's|^\./||' | LC_ALL=C sort |
    while IFS= read -r file; do echo "$(sha256_of "$file")  $file"; done) > "$dir/SHA256SUMS"
  {
    echo '<!doctype html><html lang="en"><head><meta charset="utf-8"><title>SoundTouch source (LGPL-2.1)</title></head><body>'
    echo "<h1>SoundTouch $(cfg SOUNDTOUCH_VERSION) in StemStudio (LGPL-2.1)</h1>"
    echo '<p>Complete corresponding source of js/soundtouch/soundtouch.wasm: read NOTICE.md and README.md (rebuild and replace the module).</p><ul>'
    while IFS= read -r file; do echo "<li><a href=\"${file##* }\">${file##* }</a> <code>${file%% *}</code></li>"; done < "$dir/SHA256SUMS"
    echo '<li><a href="SHA256SUMS">SHA256SUMS</a></li></ul></body></html>'
  } > "$dir/index.html"
}

package() {
  require_shim
  verify_archive
  rm -rf "$PACKAGE_DIR"
  package_files "$PACKAGE_DIR"
  package_index "$PACKAGE_DIR"
  echo "packaged $(wc -l < "$PACKAGE_DIR/SHA256SUMS" | tr -d ' ') files into $PACKAGE_DIR"
}

main() {
  parse_args "$@"
  load_lock
  resolve_paths
  case "$MODE" in
    print-config) print_config ;;
    fetch-only) fetch_archive ;;
    check-pins) check_pins ;;
    package) package ;;
    local) build_local ;;
    verify-reproducible) verify_reproducible ;;
    *) build_docker ;;
  esac
}

main "$@"
