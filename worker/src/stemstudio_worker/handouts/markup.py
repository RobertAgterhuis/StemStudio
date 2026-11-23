"""Red stub (020 T068/T070/T072)."""

from pathlib import Path
from typing import Any

STYLE = Path(__file__).parent / "styles" / "handout.css"


def css_string(*args: Any, **kwargs: Any) -> Any:
    """Stub."""
    raise NotImplementedError


def render_html(*args: Any, **kwargs: Any) -> Any:
    """Stub."""
    raise NotImplementedError
