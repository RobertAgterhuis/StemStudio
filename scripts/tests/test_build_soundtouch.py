"""Tests for scripts/build-soundtouch.{sh,ps1} (feature 012 T010, ADR-021 P1-P4).

The compile itself needs the emsdk image and runs in the CI job ``soundtouch``; these tests cover
everything around it without network or Docker: lock parsing, the SHA-256 gate, the shim check,
the LGPL distribution folder, the image pin guard and the bash / PowerShell parity.
"""

import hashlib
import shutil
import subprocess
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "build-soundtouch.sh"
PS_SCRIPT = REPO_ROOT / "scripts" / "build-soundtouch.ps1"
REAL_LOCK = REPO_ROOT / "third_party" / "soundtouch" / "soundtouch.lock"
BASH = shutil.which("bash")
PWSH = shutil.which("pwsh")
ARCHIVE_BYTES = b"not really a tarball, but bytes with a known hash"
UNREACHABLE = "https://127.0.0.1:9/soundtouch-test.tar.gz"

pytestmark = pytest.mark.skipif(BASH is None, reason="bash not available")


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def lock_text(digest: str, url: str = UNREACHABLE, skip: str = "") -> str:
    values = {
        "SOUNDTOUCH_VERSION": "2.4.1",
        "SOUNDTOUCH_URL": url,
        "SOUNDTOUCH_SHA256": digest,
        "SOUNDTOUCH_ARCHIVE_NAME": "soundtouch-test.tar.gz",
        "SOUNDTOUCH_ARCHIVE_ROOT": "soundtouch",
        "EMSDK_IMAGE": "emscripten/emsdk:6.0.13",
        "EMSDK_DIGEST": "sha256:" + "0" * 64,
        "SOUNDTOUCH_SOURCES": "AAFilter.cpp SoundTouch.cpp",
        "EMCC_FLAGS": "-O3 -DSOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER=1",
        "OUTPUT": "build/test/soundtouch.wasm",
        "ABI_VERSION": "1",
    }
    lines = ["# test lock", ""] + [f"{k}={v}" for k, v in values.items() if k != skip]
    return "\r\n".join(lines) + "\r\n"  # CRLF: a Windows checkout must parse too


def make_lock(tmp_path: Path, text: str, archive: bytes | None = ARCHIVE_BYTES) -> Path:
    lock_dir = tmp_path / "soundtouch"
    (lock_dir / "include").mkdir(parents=True)
    (lock_dir / "include" / "stemstudio_soundtouch.h").write_text("/* abi */\n")
    for name in ("COPYING.TXT", "NOTICE.md", "README.md"):
        (lock_dir / name).write_text(f"{name}\n")
    if archive is not None:
        (lock_dir / "upstream").mkdir()
        (lock_dir / "upstream" / "soundtouch-test.tar.gz").write_bytes(archive)
    lock = lock_dir / "soundtouch.lock"
    lock.write_bytes(text.encode())
    return lock


def make_shim(tmp_path: Path) -> Path:
    shim = tmp_path / "shim"
    shim.mkdir()
    (shim / "stemstudio_soundtouch.cpp").write_text("// shim\n")
    return shim


def run_sh(*args: str | Path) -> subprocess.CompletedProcess[str]:
    rest = [a.as_posix() if isinstance(a, Path) else a for a in args]
    argv = [str(BASH), SCRIPT.as_posix(), *rest]
    return subprocess.run(argv, capture_output=True, text=True, check=False, timeout=60)  # noqa: S603


def test_print_config_lists_every_lock_value(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES)))
    result = run_sh("--print-config", "--lock", lock)
    assert result.returncode == 0, result.stderr
    assert "SOUNDTOUCH_SHA256=" + sha256(ARCHIVE_BYTES) + "\n" in result.stdout
    assert "EMCC_FLAGS=-O3 -DSOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER=1\n" in result.stdout
    assert "ARCHIVE=" in result.stdout
    assert result.stdout.rstrip().endswith("build/test/soundtouch.wasm")


@pytest.mark.parametrize("key", ["SOUNDTOUCH_SHA256", "EMSDK_DIGEST", "ABI_VERSION"])
def test_missing_lock_key_fails(tmp_path: Path, key: str) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES), skip=key))
    result = run_sh("--print-config", "--lock", lock)
    assert result.returncode == 1
    assert f"lock key missing or empty: {key}" in result.stderr


def test_invalid_lock_line_fails(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES)) + "lower=case\r\n")
    result = run_sh("--print-config", "--lock", lock)
    assert result.returncode == 1
    assert "invalid lock line: lower=case" in result.stderr


def test_missing_lock_file_fails(tmp_path: Path) -> None:
    result = run_sh("--print-config", "--lock", tmp_path / "absent.lock")
    assert result.returncode == 1
    assert "lock file not found" in result.stderr


def test_matching_archive_is_accepted_without_network(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES)))
    result = run_sh("--fetch-only", "--lock", lock)
    assert result.returncode == 0, result.stderr
    assert f"verified soundtouch-test.tar.gz sha256={sha256(ARCHIVE_BYTES)}" in result.stdout


def test_mismatching_archive_is_deleted_and_fails(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text("a" * 64))
    result = run_sh("--fetch-only", "--lock", lock)
    assert result.returncode == 1
    assert "SHA-256 mismatch" in result.stderr
    assert sha256(ARCHIVE_BYTES) in result.stderr
    assert not (lock.parent / "upstream" / "soundtouch-test.tar.gz").exists()


def test_download_failure_leaves_no_partial_file(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text("a" * 64), archive=None)
    result = run_sh("--fetch-only", "--lock", lock)
    assert result.returncode == 1
    assert f"download failed: {UNREACHABLE}" in result.stderr
    assert list((lock.parent / "upstream").iterdir()) == []


def test_plain_http_is_refused(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text("a" * 64, url="http://127.0.0.1:9/x.tar.gz"), archive=None)
    result = run_sh("--fetch-only", "--lock", lock)
    assert result.returncode == 1
    assert "download failed" in result.stderr


@pytest.mark.parametrize("mode", ["--local", "--verify-reproducible"])
def test_build_needs_the_shim_before_anything_else(tmp_path: Path, mode: str) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES)))
    result = run_sh(mode, "--lock", lock, "--shim-dir", tmp_path / "no-shim")
    assert result.returncode == 1
    assert "shim not found" in result.stderr
    assert "012 T020" in result.stderr


def test_unknown_option_is_a_usage_error() -> None:
    result = run_sh("--frobnicate")
    assert result.returncode == 2
    assert "unknown option: --frobnicate" in result.stderr


def test_help_documents_every_mode_and_the_non_docker_path() -> None:
    result = run_sh("--help")
    assert result.returncode == 0
    for option in ("--local", "--verify-reproducible", "--fetch-only", "--package", "--check-pins"):
        assert option in result.stdout
    assert "non-Docker path" in result.stdout
    assert not result.stdout.startswith("#")


def test_package_writes_the_lgpl_distribution_folder(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES)))
    dist = tmp_path / "dist"
    result = run_sh("--package", dist, "--lock", lock, "--shim-dir", make_shim(tmp_path))
    assert result.returncode == 0, result.stderr
    expected = {
        "COPYING.TXT", "NOTICE.md", "README.md", "soundtouch.lock", "soundtouch-test.tar.gz",
        "include/stemstudio_soundtouch.h", "shim/stemstudio_soundtouch.cpp",
        "scripts/build-soundtouch.sh", "scripts/build-soundtouch.ps1",
    }  # fmt: skip
    lines = (dist / "SHA256SUMS").read_text().splitlines()
    sums = {name: digest for digest, name in (line.split("  ", 1) for line in lines)}
    assert set(sums) == expected
    assert all(sums[name] == sha256((dist / name).read_bytes()) for name in expected)
    index = (dist / "index.html").read_text()
    assert all(f'<a href="{name}">' in index for name in expected)
    assert "LGPL-2.1" in index


def test_package_refuses_an_unverified_archive(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text("b" * 64))
    shim = make_shim(tmp_path)
    result = run_sh("--package", tmp_path / "dist", "--lock", lock, "--shim-dir", shim)
    assert result.returncode == 1
    assert "SHA-256 mismatch" in result.stderr
    assert not (tmp_path / "dist").exists()


def test_web_image_pins_the_lock_emsdk_digest() -> None:
    result = run_sh("--check-pins")
    assert result.returncode == 0, result.stderr
    assert "pins ok" in result.stdout


def test_pin_drift_between_lock_and_web_image_fails(tmp_path: Path) -> None:
    lock = make_lock(tmp_path, lock_text(sha256(ARCHIVE_BYTES)))
    result = run_sh("--check-pins", "--lock", lock)
    assert result.returncode == 1
    assert "docker/web.Dockerfile must pin" in result.stderr


def test_real_lock_keeps_the_phase0_decisions() -> None:
    values = dict(
        line.split("=", 1)
        for line in REAL_LOCK.read_text().splitlines()
        if line and not line.startswith("#")
    )
    flags = values["EMCC_FLAGS"].split()
    assert "-DSOUNDTOUCH_PREVENT_CLICK_AT_RATE_CROSSOVER=1" in flags  # ADR-021 P3
    assert "-msimd128" not in flags  # P4
    assert values["EMSDK_IMAGE"] == "emscripten/emsdk:6.0.13"  # P1
    assert values["SOUNDTOUCH_URL"].startswith("https://www.surina.net/")  # P2


@pytest.mark.skipif(PWSH is None, reason="pwsh not available")
def test_powershell_twin_parses_the_lock_identically() -> None:
    def lock_lines(text: str) -> list[str]:
        paths = ("ARCHIVE=", "SHIM_DIR=", "OUT=")
        return [line for line in text.splitlines() if not line.startswith(paths)]

    argv = [str(PWSH), "-NoProfile", "-File", str(PS_SCRIPT), "-PrintConfig"]
    ps = subprocess.run(argv, capture_output=True, text=True, check=False, timeout=60)  # noqa: S603
    assert ps.returncode == 0, ps.stderr
    assert lock_lines(ps.stdout) == lock_lines(run_sh("--print-config").stdout)
