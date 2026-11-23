"""Tests for scripts/check-web-assets.sh (feature 016 T011, ADR-032, engine contract 2.6.0).

The web image serves the engine bundle and every AudioWorklet module the engine loads by URL with
``text/javascript``: ``audio-engine.js``, ``time-stretch-processor.js`` (012),
``audio-engine-capture-worklet.js`` (015) and ``audio-engine-pitch-worklet.js`` (016, built by
``vite.pitch.config.ts``), and the pitch module is the real build (it registers
``stemstudio-pitch-tracker``). A local ``http.server`` on a random port plays the web app.
"""

import functools
import shutil
import subprocess
import threading
from collections.abc import Iterator
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "check-web-assets.sh"
BASH = shutil.which("bash")
PITCH = b'class P{};registerProcessor("stemstudio-pitch-tracker",P);\n'
FILES = {
    "audio-engine.js": b"export const engine = 1;\n",
    "time-stretch-processor.js": b"registerProcessor('stemstudio-time-stretch', S);\n",
    "audio-engine-capture-worklet.js": b"registerProcessor('stemstudio-capture', C);\n",
    "audio-engine-pitch-worklet.js": PITCH,
}

pytestmark = pytest.mark.skipif(BASH is None, reason="bash not available")


class JsHandler(SimpleHTTPRequestHandler):
    """Serves ``.js`` as ``text/javascript`` (as ASP.NET static assets do) and ``.bin`` as bytes."""

    extensions_map = {".js": "text/javascript", ".bin": "application/octet-stream"}  # noqa: RUF012

    def log_message(self, format: str, *args: object) -> None:  # noqa: A002
        """Keeps the test output free of access log lines."""


@pytest.fixture
def site(tmp_path: Path) -> Iterator[tuple[Path, str]]:
    root = tmp_path / "wwwroot"
    (root / "js").mkdir(parents=True)
    for name, data in FILES.items():
        (root / "js" / name).write_bytes(data)
    server = ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(JsHandler, directory=root))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    yield root, f"http://127.0.0.1:{server.server_address[1]}"
    server.shutdown()
    server.server_close()


def run(*args: str) -> subprocess.CompletedProcess[str]:
    argv = [str(BASH), SCRIPT.as_posix(), *args]
    return subprocess.run(argv, capture_output=True, text=True, check=False, timeout=60)  # noqa: S603


def test_every_engine_module_served_as_javascript_passes(site: tuple[Path, str]) -> None:
    result = run(site[1])

    assert result.returncode == 0, result.stdout
    for name in FILES:
        assert f"ok   js/{name} (200 text/javascript" in result.stdout
    assert (
        "ok   js/audio-engine-pitch-worklet.js registers stemstudio-pitch-tracker" in result.stdout
    )
    assert "all web asset checks passed" in result.stdout


def test_a_missing_pitch_worklet_fails(site: tuple[Path, str]) -> None:
    (site[0] / "js" / "audio-engine-pitch-worklet.js").unlink()

    result = run(site[1])

    assert result.returncode == 1
    assert (
        "FAIL js/audio-engine-pitch-worklet.js: expected 200 text/javascript, got 404"
        in result.stdout
    )


def test_a_wrong_content_type_fails(
    site: tuple[Path, str], monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(JsHandler, "extensions_map", {".js": "application/octet-stream"})

    result = run(site[1])

    assert result.returncode == 1
    assert (
        result.stdout.count("expected 200 text/javascript, got 200 application/octet-stream") == 4
    )


def test_a_pitch_worklet_that_is_not_the_real_build_fails(site: tuple[Path, str]) -> None:
    (site[0] / "js" / "audio-engine-pitch-worklet.js").write_bytes(b"export {};\n")

    result = run(site[1])

    assert result.returncode == 1
    assert (
        "FAIL js/audio-engine-pitch-worklet.js does not register stemstudio-pitch-tracker"
        in result.stdout
    )


def test_an_unreachable_server_fails_every_module() -> None:
    result = run("http://127.0.0.1:9")

    assert result.returncode == 1
    assert result.stdout.count("got 000") == len(FILES)


def test_usage() -> None:
    assert run("--bogus").returncode == 2
    assert "BASE_URL" in run("--help").stdout
