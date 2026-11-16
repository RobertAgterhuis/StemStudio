"""Tests for scripts/check-soundtouch-served.sh (feature 012 T067, FR-006, ADR-021 D7).

A local ``http.server`` on a random port plays the web app; the tree it serves is built per test.
"""

import functools
import hashlib
import shutil
import subprocess
import threading
from collections.abc import Iterator
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "check-soundtouch-served.sh"
BASH = shutil.which("bash")
ARCHIVE = b"archive bytes"
WASM = b"\0asm\1\0\0\0"
FILES = {
    "COPYING.TXT": b"LGPL-2.1\n",
    "NOTICE.md": b"notice\n",
    "README.md": b"readme\n",
    "soundtouch.lock": b"lock\n",
    "soundtouch-test.tar.gz": ARCHIVE,
    "include/stemstudio_soundtouch.h": b"/* abi */\n",
    "shim/stemstudio_soundtouch.cpp": b"// shim\n",
    "scripts/build-soundtouch.sh": b"#!/bin/sh\n",
    "scripts/build-soundtouch.ps1": b"# ps\n",
}

pytestmark = pytest.mark.skipif(BASH is None, reason="bash not available")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, format: str, *args: object) -> None:  # noqa: A002
        """Keeps the test output free of access log lines."""


@pytest.fixture
def site(tmp_path: Path) -> Iterator[tuple[Path, str]]:
    root = tmp_path / "wwwroot"
    dist = root / "third-party" / "soundtouch"
    for name, data in FILES.items():
        (dist / name).parent.mkdir(parents=True, exist_ok=True)
        (dist / name).write_bytes(data)
    sums = "".join(f"{sha256(data)}  {name}\n" for name, data in sorted(FILES.items()))
    (dist / "SHA256SUMS").write_text(sums)
    (dist / "index.html").write_text("<!doctype html><title>SoundTouch</title>")
    (root / "js" / "soundtouch").mkdir(parents=True)
    (root / "js" / "soundtouch" / "soundtouch.wasm").write_bytes(WASM)
    server = ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(QuietHandler, directory=root))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    yield root, f"http://127.0.0.1:{server.server_address[1]}"
    server.shutdown()
    server.server_close()


@pytest.fixture
def lock(tmp_path: Path) -> Path:
    path = tmp_path / "soundtouch.lock"
    lines = [
        "# lock",
        "SOUNDTOUCH_ARCHIVE_NAME=soundtouch-test.tar.gz",
        f"SOUNDTOUCH_SHA256={sha256(ARCHIVE)}",
    ]
    path.write_bytes(("\r\n".join(lines) + "\r\n").encode())
    return path


def run(*args: str | Path) -> subprocess.CompletedProcess[str]:
    rest = [a.as_posix() if isinstance(a, Path) else a for a in args]
    argv = [str(BASH), SCRIPT.as_posix(), *rest]
    return subprocess.run(argv, capture_output=True, text=True, check=False, timeout=60)  # noqa: S603


def test_complete_distribution_passes(site: tuple[Path, str], lock: Path, tmp_path: Path) -> None:
    expected = tmp_path / "expected.wasm"
    expected.write_bytes(WASM)
    result = run("--lock", lock, "--expect-wasm", expected, site[1])
    assert result.returncode == 0, result.stdout
    assert "ok   js/soundtouch/soundtouch.wasm (200 application/wasm)" in result.stdout
    assert "soundtouch.wasm equals" in result.stdout
    assert result.stdout.count("sha256\n") == len(FILES)
    assert "all soundtouch checks passed" in result.stdout


def test_tampered_file_fails(site: tuple[Path, str], lock: Path) -> None:
    (site[0] / "third-party" / "soundtouch" / "NOTICE.md").write_text("changed\n")
    result = run("--lock", lock, site[1])
    assert result.returncode == 1
    assert "FAIL third-party/soundtouch/NOTICE.md: SHA-256 differs from SHA256SUMS" in result.stdout


def test_missing_listed_file_fails(site: tuple[Path, str], lock: Path) -> None:
    (site[0] / "third-party" / "soundtouch" / "shim" / "stemstudio_soundtouch.cpp").unlink()
    result = run("--lock", lock, site[1])
    assert result.returncode == 1
    assert "FAIL third-party/soundtouch/shim/stemstudio_soundtouch.cpp: HTTP 404" in result.stdout


def test_unlisted_required_file_fails(site: tuple[Path, str], lock: Path) -> None:
    sums = site[0] / "third-party" / "soundtouch" / "SHA256SUMS"
    kept = [line for line in sums.read_text().splitlines() if not line.endswith("COPYING.TXT")]
    sums.write_text("\n".join(kept) + "\n")
    result = run("--lock", lock, site[1])
    assert result.returncode == 1
    assert "FAIL SHA256SUMS does not list COPYING.TXT" in result.stdout


def test_archive_other_than_the_pinned_one_fails(site: tuple[Path, str], tmp_path: Path) -> None:
    other = tmp_path / "other.lock"
    other.write_text(
        f"SOUNDTOUCH_ARCHIVE_NAME=soundtouch-test.tar.gz\nSOUNDTOUCH_SHA256={'c' * 64}\n"
    )
    result = run("--lock", other, site[1])
    assert result.returncode == 1
    assert "is not the archive pinned in soundtouch.lock" in result.stdout


def test_different_wasm_and_missing_module_fail(
    site: tuple[Path, str], lock: Path, tmp_path: Path
) -> None:
    expected = tmp_path / "expected.wasm"
    expected.write_bytes(WASM + b"x")
    (site[0] / "js" / "soundtouch" / "soundtouch.wasm").unlink()
    result = run("--lock", lock, "--expect-wasm", expected, site[1])
    assert result.returncode == 1
    assert (
        "FAIL js/soundtouch/soundtouch.wasm: expected 200 application/wasm, got 404"
        in result.stdout
    )
    assert "FAIL soundtouch.wasm differs from" in result.stdout


def test_unreachable_server_fails_every_check(lock: Path) -> None:
    result = run("--lock", lock, "http://127.0.0.1:9")
    assert result.returncode == 1
    assert (
        "FAIL js/soundtouch/soundtouch.wasm: expected 200 application/wasm, got 000"
        in result.stdout
    )
    assert "FAIL third-party/soundtouch/SHA256SUMS: HTTP 000" in result.stdout


def test_usage_errors(tmp_path: Path) -> None:
    assert run("--bogus").returncode == 2
    missing = run("--lock", tmp_path / "absent.lock")
    assert missing.returncode == 2
    assert "lock file not found" in missing.stderr
    assert "BASE_URL" in run("--help").stdout
    absent_wasm = run("--expect-wasm", tmp_path / "absent.wasm")
    assert absent_wasm.returncode == 2
    assert "file not found" in absent_wasm.stderr
