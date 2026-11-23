"""The files a handout render may read: packaged assets and the job's artwork (FR-018, ADR-029 D7).

An :class:`AllowList` maps the ``file:`` URI of every allowed file to its real path. A URL is
served only when it names one of these files exactly: no other scheme (``http(s)``, ``data:``,
``ftp``), no host (``file://server/share`` would be a network path on Windows), no query or
fragment, no ``..`` and no percent-encoded detour. The file must still be a regular file at the
same real path when it is read (a symlink swapped in later is refused) and at most
``MAX_RESOURCE_BYTES`` large. Notation PDFs are merged by pypdf, never fetched by the layout, so
they are not on the list.
"""

import stat
import warnings
from collections.abc import Iterable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Final
from urllib.parse import unquote, urlsplit
from urllib.request import url2pathname

from PIL import Image

from stemstudio_worker.ports import ARTWORK_PATTERN

ASSETS_DIR: Final = Path(__file__).resolve().parent / "assets"
"""Packaged copies of the app tokens and the Archivo font (``scripts/sync_handout_style.py``)."""

ARCHIVO_FONT: Final = ASSETS_DIR / "fonts" / "archivo" / "Archivo-Variable-latin.woff2"
"""Archivo variable font (OFL-1.1), Latin subset."""

TOKENS_CSS: Final = ASSETS_DIR / "tokens.css"
"""The app's light-theme tokens (``--ss-*``)."""

MAX_RESOURCE_BYTES: Final = 16 * 1024 * 1024
"""Largest file the layout may read (a cover is far smaller)."""

MAX_ARTWORK_PIXELS: Final = 40_000_000
"""Largest artwork in pixels (a 005 cover is at most a few megapixels); larger is left out."""

CONTENT_TYPES: Final[Mapping[str, str]] = {
    ".woff2": "font/woff2",
    ".jpg": "image/jpeg",
}
"""The only file types the layout may read, by suffix."""


def artwork_fits(path: Path) -> bool:
    """Whether ``path`` is a JPEG of at most ``MAX_ARTWORK_PIXELS`` (only its header is read)."""
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", Image.DecompressionBombWarning)
            with Image.open(path) as image:
                return image.format == "JPEG" and image.width * image.height <= MAX_ARTWORK_PIXELS
    except Exception:  # any failure of an untrusted picture leaves it out
        return False


class RefusedResourceError(ValueError):
    """A URL or file is not on the allow-list (WeasyPrint logs it and goes on without it)."""


def _refused(what: str) -> RefusedResourceError:
    return RefusedResourceError(f"Not an allowed handout resource: {what[:200]}")


@dataclass(frozen=True, slots=True)
class AllowList:
    """The real paths a render may read, by their ``file:`` URI."""

    paths: frozenset[Path]

    @classmethod
    def of(cls, paths: Iterable[Path]) -> "AllowList":
        """Allow ``paths`` (absolute real paths of a known type) and the packaged font.

        Raises:
            RefusedResourceError: A path is relative or not a known resource type.
        """
        allowed = frozenset((ARCHIVO_FONT, *paths))
        for path in allowed:
            if not _plain_path(path):
                raise _refused(str(path))
        return cls(allowed)

    @classmethod
    def for_job(cls, files: Mapping[str, Path]) -> "AllowList":
        """The packaged font plus the artwork of ``files`` (library path -> real path)."""
        return cls.of(real for path, real in files.items() if ARTWORK_PATTERN.fullmatch(path))

    def uri(self, path: Path) -> str:
        """The ``file:`` URI the layout uses for an allowed ``path``.

        Raises:
            RefusedResourceError: ``path`` is not allowed.
        """
        if path not in self.paths:
            raise _refused(str(path))
        return path.as_uri()

    def read(self, url: str) -> tuple[bytes, str]:
        """The bytes and content type of the allowed file ``url`` names.

        Raises:
            RefusedResourceError: The URL names no allowed file, or the file changed.
        """
        path = self._path_of(url)
        return _read_regular(path), CONTENT_TYPES[path.suffix]

    def _path_of(self, url: str) -> Path:
        parts = urlsplit(url)
        plain = (parts.scheme, parts.netloc, parts.query, parts.fragment) == ("file", "", "", "")
        path = Path(url2pathname(unquote(parts.path)))
        if not plain or not _NO_QUERY.isdisjoint(url) or path not in self.paths:
            raise _refused(url)
        return path


_NO_QUERY: Final = frozenset("?#")


def _plain_path(path: Path) -> bool:
    """An absolute path without ``..`` of a known resource type."""
    return path.is_absolute() and ".." not in path.parts and path.suffix in CONTENT_TYPES


def _read_regular(path: Path) -> bytes:
    """Read ``path`` if it is still a small regular file at its own real path."""
    try:
        info = path.lstat()
        same = path.resolve(strict=True) == path
    except OSError:
        raise _refused(str(path)) from None
    if not (same and stat.S_ISREG(info.st_mode)) or info.st_size > MAX_RESOURCE_BYTES:
        raise _refused(str(path))
    return path.read_bytes()
