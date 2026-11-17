#!/usr/bin/env bash
# Checks that a running StemStudio web serves the SoundTouch module and its LGPL-2.1 distribution
# folder completely and unchanged (feature 012 T067, FR-006, ADR-021 D7):
#   - /js/soundtouch/soundtouch.wasm: 200 with Content-Type application/wasm (and, with
#     --expect-wasm FILE, byte-identical to FILE, e.g. the reproducible CI build);
#   - /third-party/soundtouch/index.html: 200 text/html;
#   - /third-party/soundtouch/SHA256SUMS lists the licence, notice, readme, lock, the exact source
#     archive of soundtouch.lock, header, shim and both build scripts, and every listed file is
#     served with exactly that SHA-256; the archive's hash equals SOUNDTOUCH_SHA256 of the lock.
#
# Usage: scripts/check-soundtouch-served.sh [--expect-wasm FILE] [--lock FILE] [BASE_URL]
#   BASE_URL  default http://localhost:8080
# Exit codes: 0 all checks passed, 1 a check failed (all failures are listed), 2 usage error.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCK="$REPO_ROOT/third_party/soundtouch/soundtouch.lock"
BASE=http://localhost:8080
EXPECT_WASM=""
FAILURES=0
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --expect-wasm) EXPECT_WASM="${2:?--expect-wasm needs a file}"; shift ;;
      --lock) LOCK="${2:?--lock needs a file}"; shift ;;
      -h | --help) sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d; s/^# \{0,1\}//'; exit 0 ;;
      -*) echo "check-soundtouch-served: unknown option: $1" >&2; exit 2 ;;
      *) BASE="${1%/}" ;;
    esac
    shift
  done
}

fail() { echo "FAIL $*"; FAILURES=$((FAILURES + 1)); }
pass() { echo "ok   $*"; }
lock_value() { sed -n "s/^$1=//p" "$LOCK" | tr -d '\r'; }
sha256_of() {
  if command -v sha256sum > /dev/null; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi
}

# fetch <path> <file>: prints "<status> <content type>" and stores the body in <file>.
fetch() { curl --silent --show-error --output "$2" --write-out '%{http_code} %{content_type}' "$BASE/$1" || echo "000 -"; }

check_type() {
  local path="$1" want="$2" got
  got="$(fetch "$path" "$TMP/body")"
  if [[ "$got" == "200 $want"* ]]; then pass "$path ($got)"; else fail "$path: expected 200 $want, got $got"; fi
}

check_wasm() {
  check_type js/soundtouch/soundtouch.wasm application/wasm
  [[ -n "$EXPECT_WASM" ]] || return 0
  if [[ "$(sha256_of "$TMP/body")" == "$(sha256_of "$EXPECT_WASM")" ]]; then
    pass "soundtouch.wasm equals $EXPECT_WASM"
  else
    fail "soundtouch.wasm differs from $EXPECT_WASM"
  fi
}

check_listed_file() {
  local digest="$1" name="$2" status
  status="$(fetch "third-party/soundtouch/$name" "$TMP/file")"
  if [[ "${status%% *}" != 200 ]]; then fail "third-party/soundtouch/$name: HTTP ${status%% *}"; return; fi
  if [[ "$(sha256_of "$TMP/file")" == "$digest" ]]; then pass "third-party/soundtouch/$name sha256"; else fail "third-party/soundtouch/$name: SHA-256 differs from SHA256SUMS"; fi
}

check_required() {
  local name archive
  archive="$(lock_value SOUNDTOUCH_ARCHIVE_NAME)"
  for name in COPYING.TXT NOTICE.md README.md soundtouch.lock "$archive" include/stemstudio_soundtouch.h \
    shim/stemstudio_soundtouch.cpp scripts/build-soundtouch.sh scripts/build-soundtouch.ps1; do
    grep -q "  $name\$" "$TMP/sums" || fail "SHA256SUMS does not list $name"
  done
  grep -q "^$(lock_value SOUNDTOUCH_SHA256)  $archive\$" "$TMP/sums" ||
    fail "$archive is not the archive pinned in soundtouch.lock"
}

check_distribution() {
  local status digest name
  check_type third-party/soundtouch/index.html text/html
  status="$(fetch third-party/soundtouch/SHA256SUMS "$TMP/sums")"
  if [[ "${status%% *}" != 200 ]]; then fail "third-party/soundtouch/SHA256SUMS: HTTP ${status%% *}"; return; fi
  tr -d '\r' < "$TMP/sums" > "$TMP/sums.lf" && mv "$TMP/sums.lf" "$TMP/sums"
  check_required
  while read -r digest name; do check_listed_file "$digest" "$name"; done < "$TMP/sums"
}

main() {
  parse_args "$@"
  [[ -f "$LOCK" ]] || { echo "check-soundtouch-served: lock file not found: $LOCK" >&2; exit 2; }
  [[ -z "$EXPECT_WASM" || -f "$EXPECT_WASM" ]] || { echo "check-soundtouch-served: file not found: $EXPECT_WASM" >&2; exit 2; }
  echo "checking $BASE"
  check_wasm
  check_distribution
  if [[ $FAILURES -gt 0 ]]; then echo "$FAILURES check(s) failed"; exit 1; fi
  echo "all soundtouch checks passed"
}

main "$@"
