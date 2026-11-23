"""Allow-list of the files a handout render may read (feature 020, FR-018). Red stub."""

from collections.abc import Iterable, Mapping
from pathlib import Path


class RefusedResourceError(ValueError):
    """Stub."""


class AllowList:
    """Stub."""

    @classmethod
    def of(cls, paths: Iterable[Path]) -> "AllowList":
        """Stub."""
        raise NotImplementedError

    @classmethod
    def for_job(cls, files: Mapping[str, Path]) -> "AllowList":
        """Stub."""
        raise NotImplementedError

    def uri(self, path: Path) -> str:
        """Stub."""
        raise NotImplementedError

    def read(self, url: str) -> tuple[bytes, str]:
        """Stub."""
        raise NotImplementedError


ARCHIVO_FONT = (
    Path(__file__).resolve().parent
    / "assets"
    / "fonts"
    / "archivo"
    / "Archivo-Variable-latin.woff2"
)
MAX_RESOURCE_BYTES = 0
