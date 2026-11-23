"""The handout HTML: Jinja2 templates with autoescape over a ``HandoutView`` (FR-012, FR-018).

Every value of the view is text and is escaped; only the packaged style sheets (the app tokens and
``styles/handout.css``) and the page style built here are inserted unescaped (an ``autoescape
false`` block around the ``<style>`` elements of the template). The page style
holds the paper size, the notation frame height, the Archivo font face (its allowed ``file:``
URI) and the footer notice as a CSS string.
"""

import functools
from pathlib import Path
from typing import Final

from jinja2 import Environment, FileSystemLoader, StrictUndefined

from stemstudio_worker.handouts.document import HandoutView
from stemstudio_worker.handouts.resources import TOKENS_CSS
from stemstudio_worker.ports import NOTICE_FOOTER

PACKAGE: Final = Path(__file__).resolve().parent
TEMPLATES: Final = PACKAGE / "templates"
STYLE: Final = PACKAGE / "styles" / "handout.css"


@functools.cache
def _environment() -> Environment:
    return Environment(loader=FileSystemLoader(TEMPLATES), autoescape=True,
                       undefined=StrictUndefined, keep_trailing_newline=True)  # fmt: skip


@functools.cache
def _sheets() -> tuple[str, str]:
    return TOKENS_CSS.read_text(encoding="utf-8"), STYLE.read_text(encoding="utf-8")


def css_string(text: str) -> str:
    """``text`` as a double-quoted CSS string (backslash, quote and line breaks escaped)."""
    escaped = text.replace("\\", "\\\\").replace('"', '\\"')
    return '"' + escaped.replace("\n", "\\a ").replace("\r", "") + '"'


def page_css(view: HandoutView, font_uri: str) -> str:
    """The per-handout style: paper, frame height, font face, footer notice."""
    return (
        f"@page {{ size: {view.paper};"
        f" @bottom-left {{ content: {css_string(NOTICE_FOOTER)}; }} }}\n"
        f".frame {{ height: {view.frame_height_mm}mm; }}\n"
        f'@font-face {{ font-family: "Archivo"; src: url({css_string(font_uri)}) format("woff2");'
        " font-weight: 100 900; font-stretch: 62% 125%; }\n"
    )


def render_html(view: HandoutView, font_uri: str) -> str:
    """The handout HTML of ``view`` (``font_uri``: the allowed URI of the Archivo font)."""
    tokens, css = _sheets()
    template = _environment().get_template("handout.html.j2")
    return template.render(view=view, tokens=tokens, css=css, page_css=page_css(view, font_uri))
