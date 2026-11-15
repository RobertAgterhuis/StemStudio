"""Tests of ``worker/scripts/install_music_font.py`` (architect, 008 T002); fontTools is faked."""

import base64
import hashlib
import importlib.util
import io
import sys
import types
from pathlib import Path
from typing import Any

import pytest

_SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "install_music_font.py"
_SPEC = importlib.util.spec_from_file_location("install_music_font", _SCRIPT)
assert _SPEC is not None
assert _SPEC.loader is not None
font = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(font)

_WOFF2 = b"wOF2-fake-font-bytes"
_TTF = b"\x00\x01\x00\x00rest-of-a-truetype-font"


def _css(data: bytes = _WOFF2, family: str = "Leipzig") -> str:
    encoded = base64.b64encode(data).decode("ascii")
    return (
        "/* Verovio */\n@font-face {\n"
        f"    font-family: '{family}';\n"
        "    src: url(data:application/font-woff2;charset=utf-8;base64,"
        f"{encoded}) format('woff2');\n"
        "    font-weight: normal;\n    font-style: normal;\n}\n"
    )


def _write_css(tmp_path: Path, text: str) -> Path:
    path = tmp_path / "Leipzig.css"
    path.write_text(text, encoding="utf-8")
    return path


def _to_ttf(woff2: bytes) -> bytes:
    assert woff2 == _WOFF2
    return _TTF


def test_extract_woff2_decodes_the_leipzig_face() -> None:
    assert font.extract_woff2(_css()) == _WOFF2


@pytest.mark.parametrize(
    "text", ["", _css(family="Bravura"), "src: url(data:font/ttf;base64,AAAA)"]
)
def test_extract_woff2_rejects_css_without_the_font(text: str) -> None:
    with pytest.raises(font.FontInstallError, match="no Leipzig WOFF2"):
        font.extract_woff2(text)


@pytest.mark.parametrize(("head", "name"), [(_TTF, "Leipzig.ttf"), (b"OTTO", "Leipzig.otf")])
def test_file_name_follows_the_outline_format(head: bytes, name: str) -> None:
    assert font.font_file_name(head) == name


def test_install_writes_the_converted_font(tmp_path: Path) -> None:
    target = font.install(_write_css(tmp_path, _css()), tmp_path / "out" / "fonts", _to_ttf)

    assert target == tmp_path / "out" / "fonts" / "Leipzig.ttf"
    assert target.read_bytes() == _TTF


def test_install_reports_a_missing_css(tmp_path: Path) -> None:
    with pytest.raises(font.FontInstallError, match="does not exist"):
        font.install(tmp_path / "missing.css", tmp_path, _to_ttf)


def test_install_wraps_conversion_errors(tmp_path: Path) -> None:
    def broken(_woff2: bytes) -> bytes:
        raise ImportError("No module named 'brotli'")

    with pytest.raises(font.FontInstallError, match="could not be converted: No module named"):
        font.install(_write_css(tmp_path, _css()), tmp_path, broken)


def test_install_wraps_bad_base64(tmp_path: Path) -> None:
    css = _css().replace(base64.b64encode(_WOFF2).decode("ascii"), "AAA")

    with pytest.raises(font.FontInstallError, match="could not be converted"):
        font.install(_write_css(tmp_path, css), tmp_path, _to_ttf)


class _FakeNameTable:
    def __init__(self) -> None:
        self.names: list[tuple[str, int, int, int, int]] = []

    def setName(self, value: str, name_id: int, platform: int, encoding: int, lang: int) -> None:  # noqa: N802 - fontTools API
        self.names.append((value, name_id, platform, encoding, lang))


class _FakeTTFont:
    created: "list[_FakeTTFont]" = []

    def __init__(self, stream: io.BytesIO, *, recalcTimestamp: bool = True) -> None:  # noqa: N803 - fontTools API
        self.data = stream.read()
        self.flavor: str | None = "woff2"
        self.recalc_timestamp = recalcTimestamp
        self.name = _FakeNameTable()
        _FakeTTFont.created.append(self)

    def __getitem__(self, tag: str) -> _FakeNameTable:
        assert tag == "name"
        return self.name

    def save(self, out: io.BytesIO) -> None:
        assert self.flavor is None
        out.write(_TTF + self.data)


def _install_fake_fonttools(monkeypatch: pytest.MonkeyPatch) -> None:
    _FakeTTFont.created = []
    tt_lib = types.ModuleType("fontTools.ttLib")
    tt_lib.TTFont = _FakeTTFont  # type: ignore[attr-defined]
    monkeypatch.setitem(sys.modules, "fontTools", types.ModuleType("fontTools"))
    monkeypatch.setitem(sys.modules, "fontTools.ttLib", tt_lib)


def test_fonttools_convert_drops_the_woff2_flavor(monkeypatch: pytest.MonkeyPatch) -> None:
    _install_fake_fonttools(monkeypatch)

    assert font.fonttools_convert(_WOFF2) == _TTF + _WOFF2


def test_fonttools_convert_is_deterministic_and_names_the_font(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    _install_fake_fonttools(monkeypatch)

    font.fonttools_convert(_WOFF2)

    converted = _FakeTTFont.created[0]
    assert converted.recalc_timestamp is False
    assert ("Leipzig", 6, 3, 1, 0x409) in converted.name.names
    assert ("Leipzig", 6, 1, 0, 0) in converted.name.names


def test_default_css_is_inside_the_verovio_package(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    spec = types.SimpleNamespace(origin=str(tmp_path / "verovio" / "__init__.py"))
    monkeypatch.setattr(
        font.importlib.util, "find_spec", lambda name: spec if name == "verovio" else None
    )

    assert font.default_css() == tmp_path / "verovio" / "data" / "Leipzig.css"


def test_default_css_needs_verovio(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(font.importlib.util, "find_spec", lambda _name: None)

    with pytest.raises(font.FontInstallError, match="verovio is not installed"):
        font.default_css()


def test_main_prints_the_file_and_its_digest(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    css = _write_css(tmp_path, _css())

    code = font.main([str(tmp_path / "out"), "--css", str(css)], _to_ttf)

    expected = f"{tmp_path / 'out' / 'Leipzig.ttf'} sha256={hashlib.sha256(_TTF).hexdigest()}"
    assert code == 0
    assert capsys.readouterr().out.strip() == expected


def test_main_uses_the_installed_wheel_by_default(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    css = _write_css(tmp_path, _css())
    monkeypatch.setattr(font, "default_css", lambda: css)

    assert font.main([str(tmp_path / "out")], _to_ttf) == 0
    assert "Leipzig.ttf sha256=" in capsys.readouterr().out


@pytest.mark.parametrize("argv", [[], ["a", "b"], ["a", "--font", "b"], ["a", "--css", "b", "c"]])
def test_main_reports_usage_errors(argv: list[str], capsys: pytest.CaptureFixture[str]) -> None:
    assert font.main(argv, _to_ttf) == 2
    assert "usage:" in capsys.readouterr().err


def test_main_reports_install_errors(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert font.main([str(tmp_path), "--css", str(tmp_path / "missing.css")], _to_ttf) == 1
    assert "error:" in capsys.readouterr().err


def test_module_documents_the_licence_and_the_target_directory() -> None:
    doc: Any = font.__doc__

    assert "OFL" in doc
    assert "/usr/local/share/fonts/verovio/" in doc
