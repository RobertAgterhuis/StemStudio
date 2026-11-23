"""Tests of ``worker/scripts/sync_handout_style.py`` (architect, 020 Phase 0 T007, FR-012)."""

import hashlib
import importlib.util
import json
from pathlib import Path

import pytest

_SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "sync_handout_style.py"
_SPEC = importlib.util.spec_from_file_location("sync_handout_style", _SCRIPT)
assert _SPEC is not None
assert _SPEC.loader is not None
sync = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(sync)

_TOKENS = """/* header */
:root {
  --ss-font: "Archivo", system-ui, sans-serif;
  --ss-space-2: 0.5rem;
}

/* ---- Dark theme ---- */
:root,
html[data-theme="dark"] {
  color-scheme: dark;
  --ss-color-ink: #E6EAF0;
}

/* ---- Light theme ("day session") ---- */
html[data-theme="light"] {
  color-scheme: light;
  --ss-color-ink: #172030;
}

@media (forced-colors: active) {
  :root,
  html[data-theme] {
    --ss-color-ink: CanvasText;
  }
}
"""


def _repo(tmp_path: Path, tokens: str = _TOKENS) -> Path:
    web = tmp_path / "src" / "StemStudio.Web" / "wwwroot"
    (web / "css").mkdir(parents=True)
    (web / "css" / "tokens.css").write_text(tokens, encoding="utf-8", newline="\n")
    fonts = web / "fonts" / "archivo"
    fonts.mkdir(parents=True)
    (fonts / "Archivo-Variable-latin.woff2").write_bytes(b"wOF2-archivo")
    (fonts / "OFL.txt").write_text("SIL Open Font License 1.1\n", encoding="utf-8")
    return tmp_path


def _assets(root: Path) -> Path:
    return root / "worker" / "src" / "stemstudio_worker" / "handouts" / "assets"


def test_light_tokens_keep_the_shared_and_the_light_block_only() -> None:
    css = sync.handout_tokens(_TOKENS)

    assert '--ss-font: "Archivo", system-ui, sans-serif;' in css
    assert "--ss-color-ink: #172030;" in css
    assert "#E6EAF0" not in css
    assert "CanvasText" not in css
    assert "data-theme" not in css
    assert css.count(":root {") == 2


@pytest.mark.parametrize(
    "broken",
    [
        _TOKENS.replace('html[data-theme="light"] {', "html.light {"),
        _TOKENS.replace(":root {\n  --ss-font", ":root{\n  --ss-font"),
    ],
)
def test_missing_blocks_fail_loudly(broken: str) -> None:
    with pytest.raises(sync.StyleSyncError, match="block"):
        sync.handout_tokens(broken)


def test_sync_writes_the_copies_and_a_manifest_with_hashes(tmp_path: Path) -> None:
    root = _repo(tmp_path)

    assert sync.main(["--root", str(root)]) == 0

    assets = _assets(root)
    manifest = json.loads((assets / "manifest.json").read_text(encoding="utf-8"))
    font = assets / "fonts" / "archivo" / "Archivo-Variable-latin.woff2"
    assert font.read_bytes() == b"wOF2-archivo"
    assert (assets / "fonts" / "archivo" / "OFL.txt").is_file()
    assert (
        manifest["sources"]["src/StemStudio.Web/wwwroot/css/tokens.css"]
        == hashlib.sha256(_TOKENS.encode("utf-8")).hexdigest()
    )
    assert (
        manifest["copies"]["tokens.css"]
        == hashlib.sha256((assets / "tokens.css").read_bytes()).hexdigest()
    )
    assert set(manifest["copies"]) == {
        "tokens.css",
        "fonts/archivo/Archivo-Variable-latin.woff2",
        "fonts/archivo/OFL.txt",
    }


def test_check_passes_when_fresh_and_fails_on_drift(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    root = _repo(tmp_path)
    sync.main(["--root", str(root)])

    assert sync.main(["--root", str(root), "--check"]) == 0
    tokens = root / "src" / "StemStudio.Web" / "wwwroot" / "css" / "tokens.css"
    tokens.write_text(_TOKENS.replace("#172030", "#000000"), encoding="utf-8", newline="\n")

    assert sync.main(["--root", str(root), "--check"]) == 1
    assert "tokens.css" in capsys.readouterr().out


def test_check_fails_when_a_copy_was_edited(tmp_path: Path) -> None:
    root = _repo(tmp_path)
    sync.main(["--root", str(root)])
    (_assets(root) / "fonts" / "archivo" / "OFL.txt").write_text("edited", encoding="utf-8")

    assert sync.main(["--root", str(root), "--check"]) == 1


def test_check_fails_when_nothing_was_synced(tmp_path: Path) -> None:
    assert sync.main(["--root", str(_repo(tmp_path)), "--check"]) == 1


def test_the_committed_assets_match_the_app_sources() -> None:
    assert sync.main(["--check"]) == 0
