"""Install the Leipzig music font of the pinned Verovio wheel as a system font (008, SC-004).

Verovio writes SMuFL text glyphs (the tempo mark's note) with ``font-family="Leipzig"``; Cairo
renders them as boxes unless fontconfig knows the font (008 research.md S-08). The wheel embeds
the font only as a base64 WOFF2 inside ``verovio/data/Leipzig.css``. This build-time script
extracts it, converts it to an sfnt file with fontTools (``fontTools[woff]``, i.e. with brotli;
build stage only, never in the runtime image) and writes ``Leipzig.ttf`` (TrueType outlines;
``Leipzig.otf`` if the wheel ever ships CFF outlines). Licence: SIL OFL 1.1 (Leipzig, part of
Verovio). The devops Dockerfile then copies the file to ``/usr/local/share/fonts/verovio/`` and
runs ``fc-cache``.

Usage (build stage, with verovio and fontTools[woff] installed)::

    python worker/scripts/install_music_font.py <out_dir> [--css <path to Leipzig.css>]

Prints ``<written file> sha256=<hex>``. Exit code 0 = written, 1 = font not found or not
convertible, 2 = usage error. The output is deterministic for a given wheel (Phase 0: verovio
6.4.0, timestamps kept -> 119,416 bytes before the PostScript name was added; the image build
prints the current SHA-256).
"""

import base64
import hashlib
import importlib.util
import io
import re
import sys
from collections.abc import Callable, Sequence
from pathlib import Path
from typing import Final

FONT_FAMILY = "Leipzig"
"""Family name Verovio's SVG uses and fontconfig must list."""

_WOFF2 = re.compile(
    r"font-family:\s*'Leipzig';\s*src:\s*url\(data:application/font-woff2;charset=utf-8;base64,"
    r"(?P<data>[A-Za-z0-9+/=]+)\)\s*format\('woff2'\)"
)
_TRUETYPE = b"\x00\x01\x00\x00"

Converter = Callable[[bytes], bytes]
"""Turns WOFF2 bytes into sfnt (TTF/OTF) bytes."""


class FontInstallError(Exception):
    """The font could not be found in the CSS or not be converted."""


def extract_woff2(css: str) -> bytes:
    """Return the WOFF2 bytes of the Leipzig ``@font-face`` in ``css``.

    Raises:
        FontInstallError: No Leipzig WOFF2 data URL in the CSS.
    """
    match = _WOFF2.search(css)
    if match is None:
        msg = "Leipzig.css contains no Leipzig WOFF2 font"
        raise FontInstallError(msg)
    return base64.b64decode(match.group("data"), validate=True)


POSTSCRIPT_NAME: Final = "Leipzig"
"""PostScript name (name ID 6) written into the converted font."""


def fonttools_convert(woff2: bytes) -> bytes:
    """Decompress WOFF2 to plain sfnt bytes with fontTools (needs brotli).

    Keeps the wheel's ``head`` timestamps (deterministic output) and sets the PostScript name
    (name ID 6) so Cairo embeds the font as "Leipzig".
    """
    from fontTools.ttLib import TTFont  # noqa: PLC0415 - build-stage dependency only

    font = TTFont(io.BytesIO(woff2), recalcTimestamp=False)
    font.flavor = None
    names = font["name"]
    names.setName(POSTSCRIPT_NAME, 6, 3, 1, 0x409)
    names.setName(POSTSCRIPT_NAME, 6, 1, 0, 0)
    out = io.BytesIO()
    font.save(out)
    return out.getvalue()


def font_file_name(sfnt: bytes) -> str:
    """``Leipzig.ttf`` for TrueType outlines, ``Leipzig.otf`` for CFF (``OTTO``)."""
    return f"{FONT_FAMILY}.ttf" if sfnt[:4] == _TRUETYPE else f"{FONT_FAMILY}.otf"


def default_css() -> Path:
    """Path of ``data/Leipzig.css`` inside the installed verovio package.

    Raises:
        FontInstallError: verovio is not installed.
    """
    spec = importlib.util.find_spec("verovio")
    if spec is None or spec.origin is None:
        msg = "verovio is not installed"
        raise FontInstallError(msg)
    return Path(spec.origin).parent / "data" / "Leipzig.css"


def install(css_path: Path, out_dir: Path, convert: Converter = fonttools_convert) -> Path:
    """Extract, convert and write the font into ``out_dir``; return the written file.

    Raises:
        FontInstallError: CSS missing, no font in it, or the conversion failed.
    """
    if not css_path.is_file():
        msg = f"{css_path} does not exist"
        raise FontInstallError(msg)
    try:
        sfnt = convert(extract_woff2(css_path.read_text(encoding="utf-8")))
    except (ImportError, ValueError, OSError) as error:
        msg = f"the Leipzig font could not be converted: {error}"
        raise FontInstallError(msg) from error
    out_dir.mkdir(parents=True, exist_ok=True)
    target = out_dir / font_file_name(sfnt)
    target.write_bytes(sfnt)
    return target


def _parse(argv: Sequence[str]) -> tuple[Path, Path | None] | None:
    """``<out_dir> [--css <path>]`` -> (out_dir, css) or None for a usage error."""
    args = list(argv)
    if len(args) == 1:
        return Path(args[0]), None
    if len(args) == 3 and args[1] == "--css":  # noqa: PLR2004 - out_dir --css path
        return Path(args[0]), Path(args[2])
    return None


def main(argv: Sequence[str], convert: Converter = fonttools_convert) -> int:
    """Command line entry point; see the module docstring for arguments and exit codes."""
    parsed = _parse(argv)
    if parsed is None:
        print("usage: install_music_font.py <out_dir> [--css <Leipzig.css>]", file=sys.stderr)  # noqa: T201
        return 2
    out_dir, css = parsed
    try:
        target = install(css or default_css(), out_dir, convert)
    except FontInstallError as error:
        print(f"error: {error}", file=sys.stderr)  # noqa: T201
        return 1
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    print(f"{target} sha256={digest}")  # noqa: T201
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
