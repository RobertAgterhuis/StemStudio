"""Raw-colour guard for the StemStudio design tokens (feature 004, FR-003, design-spec.md 2).

Every colour in the web UI must come from a design token (``var(--ss-...)``). Only
``src/StemStudio.Web/wwwroot/css/tokens.css`` may contain literal colours. The check scans

* every ``*.css`` file below ``src/StemStudio.Web/wwwroot/css/`` except ``tokens.css``,
* every scoped ``*.razor.css`` file and every ``*.razor`` file of ``src/StemStudio.Web``
  (``style="..."``/``Style="..."`` attributes and ``<style>`` blocks),

and rejects hex colours (``#rgb``, ``#rgba``, ``#rrggbb``, ``#rrggbbaa``), colour functions
(``rgb()``, ``rgba()``, ``hsl()``, ``hsla()``, ``hwb()``, ``lab()``, ``lch()``, ``oklab()``,
``oklch()``, ``color()``) and the CSS named colours in declaration values. Allowed: ``var()``,
``transparent``, ``currentColor``, CSS-wide keywords and ``color-mix()`` over tokens. Selectors,
comments, strings and ``url()`` are ignored.

The legacy ``wwwroot/app.css`` (retired by task T020) is reported with warnings only.

Usage::

    python tools/quality/css_tokens_check.py [--root DIR]

Exit code 0 = clean, 1 = raw colour outside the token file.
"""

import re
import sys
from collections.abc import Iterator, Sequence
from dataclasses import dataclass
from pathlib import Path

from common import (
    Finding,
    Severity,
    build_parser,
    is_excluded,
    relative,
    report_findings,
)

WEB_DIR = "src/StemStudio.Web"
"""The Blazor web project (repository-relative)."""

TOKEN_FILE = f"{WEB_DIR}/wwwroot/css/tokens.css"
"""The only file that may define literal colours."""

LEGACY_FILES = frozenset({f"{WEB_DIR}/wwwroot/app.css"})
"""Pre-004 style sheets: checked, but reported as warnings until they are retired (T020)."""

_NAMED_COLOUR_LIST = """
    aliceblue antiquewhite aqua aquamarine azure beige bisque black blanchedalmond blue
    blueviolet brown burlywood cadetblue chartreuse chocolate coral cornflowerblue cornsilk
    crimson cyan darkblue darkcyan darkgoldenrod darkgray darkgreen darkgrey darkkhaki
    darkmagenta darkolivegreen darkorange darkorchid darkred darksalmon darkseagreen
    darkslateblue darkslategray darkslategrey darkturquoise darkviolet deeppink deepskyblue
    dimgray dimgrey dodgerblue firebrick floralwhite forestgreen fuchsia gainsboro ghostwhite
    gold goldenrod gray green greenyellow grey honeydew hotpink indianred indigo ivory khaki
    lavender lavenderblush lawngreen lemonchiffon lightblue lightcoral lightcyan
    lightgoldenrodyellow lightgray lightgreen lightgrey lightpink lightsalmon lightseagreen
    lightskyblue lightslategray lightslategrey lightsteelblue lightyellow lime limegreen linen
    magenta maroon mediumaquamarine mediumblue mediumorchid mediumpurple mediumseagreen
    mediumslateblue mediumspringgreen mediumturquoise mediumvioletred midnightblue mintcream
    mistyrose moccasin navajowhite navy oldlace olive olivedrab orange orangered orchid
    palegoldenrod palegreen paleturquoise palevioletred papayawhip peachpuff peru pink plum
    powderblue purple rebeccapurple red rosybrown royalblue saddlebrown salmon sandybrown
    seagreen seashell sienna silver skyblue slateblue slategray slategrey snow springgreen
    steelblue tan teal thistle tomato turquoise violet wheat white whitesmoke yellow
    yellowgreen
"""

NAMED_COLOURS = frozenset(_NAMED_COLOUR_LIST.split())
"""CSS Color Module 4 named colours (``transparent`` and ``currentColor`` are allowed)."""

_HEX_LENGTHS = frozenset({4, 5, 7, 9})
"""Lengths of ``#rgb``, ``#rgba``, ``#rrggbb`` and ``#rrggbbaa`` including the ``#``."""

_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)
_STRING = re.compile(r"\"(?:\\.|[^\"\\])*\"|'(?:\\.|[^'\\])*'", re.DOTALL)
_URL = re.compile(r"url\([^)]*\)", re.IGNORECASE)
_SEGMENT = re.compile(r"[^{};]+")
_PROPERTY = re.compile(r"\s*(?:--)?[A-Za-z][\w-]*\s*:")
_COLOUR = re.compile(
    r"(?P<func>(?<![\w-])(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\([^)]*\)?)"
    r"|(?P<hex>(?<![\w&-])#[0-9A-Fa-f]+(?![\w-]))"
    r"|(?P<word>(?<![\w#.-])[A-Za-z]+(?![\w(-]))",
    re.IGNORECASE,
)
_STYLE_ATTR = re.compile(
    r"(?<![\w-])style\s*=\s*(?:\"(?P<dq>[^\"]*)\"|'(?P<sq>[^']*)')", re.IGNORECASE
)
_STYLE_BLOCK = re.compile(r"<style\b[^>]*>(?P<css>.*?)</style>", re.IGNORECASE | re.DOTALL)


@dataclass(frozen=True, slots=True)
class ColourHit:
    """A raw colour literal at a 1-based line."""

    line: int
    literal: str


def find_css_colours(css: str) -> list[ColourHit]:
    """Return the raw colours in the declaration values of a style sheet or declaration list."""
    clean = _blank_all(css)
    hits: list[ColourHit] = []
    for segment in _SEGMENT.finditer(clean):
        if not clean.startswith("{", segment.end()):
            hits.extend(_declaration_hits(clean, segment.start(), segment.group()))
    return hits


def find_razor_colours(razor: str) -> list[ColourHit]:
    """Return the raw colours in ``style`` attributes and ``<style>`` blocks of a Razor file."""
    hits: list[ColourHit] = []
    for match in _STYLE_ATTR.finditer(razor):
        group = "dq" if match.group("dq") is not None else "sq"
        hits.extend(_shifted(razor, match.start(group), match.group(group)))
    for match in _STYLE_BLOCK.finditer(razor):
        hits.extend(_shifted(razor, match.start("css"), match.group("css")))
    return sorted(hits, key=lambda hit: hit.line)


def _shifted(text: str, offset: int, css: str) -> list[ColourHit]:
    base = _line_of(text, offset) - 1
    return [ColourHit(base + hit.line, hit.literal) for hit in find_css_colours(css)]


def _blank_all(css: str) -> str:
    for pattern in (_COMMENT, _STRING, _URL):
        css = pattern.sub(_blank, css)
    return css


def _blank(match: re.Match[str]) -> str:
    """Replace a match by spaces, keeping its newlines (so offsets and lines stay valid)."""
    return re.sub(r"[^\n]", " ", match.group())


def _declaration_hits(text: str, start: int, segment: str) -> Iterator[ColourHit]:
    prop = _PROPERTY.match(segment)
    if prop is None:
        return
    for match in _COLOUR.finditer(segment, prop.end()):
        if _is_raw(match):
            yield ColourHit(_line_of(text, start + match.start()), match.group())


def _is_raw(match: re.Match[str]) -> bool:
    if match.group("func"):
        return True
    if match.group("hex"):
        return len(match.group()) in _HEX_LENGTHS
    return match.group().lower() in NAMED_COLOURS


def _line_of(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def target_files(root: Path) -> list[Path]:
    """Return the style sheets and Razor files the guard scans (not tokens.css, no build output)."""
    web = root / WEB_DIR
    candidates = {
        *(web / "wwwroot" / "css").rglob("*.css"),
        *web.rglob("*.razor"),
        *web.rglob("*.razor.css"),
        *(root / name for name in LEGACY_FILES),
    }
    return sorted(path for path in candidates if _in_scope(path, root))


def _in_scope(path: Path, root: Path) -> bool:
    rel_path = relative(path, root)
    return path.is_file() and rel_path != TOKEN_FILE and not is_excluded(rel_path)


def check_file(path: Path, root: Path) -> list[Finding]:
    """Return one finding per raw colour in ``path``."""
    rel_path = relative(path, root)
    text = path.read_text(encoding="utf-8", errors="replace")
    finder = find_razor_colours if rel_path.endswith(".razor") else find_css_colours
    legacy = rel_path in LEGACY_FILES
    severity = Severity.WARNING if legacy else Severity.ERROR
    suffix = " (legacy file, retire it into css/components/)" if legacy else ""
    return [
        Finding("raw-colour", severity, rel_path, hit.line, _message(hit) + suffix, 1)
        for hit in finder(text)
    ]


def _message(hit: ColourHit) -> str:
    return f"raw colour `{hit.literal}` outside tokens.css; use a design token var(--ss-...)"


def run(root: Path) -> list[Finding]:
    """Check every target file below ``root``."""
    return [finding for path in target_files(root) for finding in check_file(path, root)]


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    args = build_parser(__doc__, with_paths=False).parse_args(argv)
    return report_findings("css_tokens_check", run(args.root))


if __name__ == "__main__":
    sys.exit(main())
