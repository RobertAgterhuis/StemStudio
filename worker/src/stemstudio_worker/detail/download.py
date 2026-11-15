"""First-use download of detail model weights with SHA-256 verification (FR-018, D-07).

The file is streamed to ``<file>.part`` while hashing, verified and then renamed, so a
half-written or tampered file is never used. Failures raise ``DetailModelUnavailableError`` with
the fixed user texts of data-model.md 4.2.
"""

import hashlib
import urllib.request
from collections.abc import Callable
from contextlib import AbstractContextManager
from http.client import HTTPException
from pathlib import Path
from typing import IO, Final, cast

from stemstudio_worker.detail.registry import ModelSpec
from stemstudio_worker.ports import DetailModelUnavailableError

DOWNLOAD_FAILED: Final = "Detail model could not be downloaded; try again when online."
"""Error text when the download fails (offline, server error, broken connection)."""

DOWNLOAD_CORRUPT: Final = "Detail model download was corrupt; try again."
"""Error text when the downloaded file does not match its SHA-256."""

TIMEOUT_SECONDS: Final = 60.0
"""Socket timeout of a download."""

_CHUNK: Final = 1024 * 1024

Opener = Callable[[str], AbstractContextManager[IO[bytes]]]
"""Opens a URL for reading (injectable for tests)."""


def open_url(url: str) -> AbstractContextManager[IO[bytes]]:
    """Open ``url`` with urllib (registry URLs are HTTPS constants)."""
    response = urllib.request.urlopen(url, timeout=TIMEOUT_SECONDS)  # noqa: S310 - constant URLs
    return cast(AbstractContextManager[IO[bytes]], response)


def sha256_of(path: Path) -> str:
    """Return the lower-case hex SHA-256 of the file at ``path``."""
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(_CHUNK):
            digest.update(chunk)
    return digest.hexdigest()


class ModelDownloader:
    """Makes model files available below ``<model_dir>/detail/<model_id>/``."""

    def __init__(self, model_dir: Path, opener: Opener = open_url) -> None:
        """Download into ``model_dir`` using ``opener``."""
        self._model_dir = model_dir
        self._opener = opener

    def path_of(self, spec: ModelSpec) -> Path:
        """Return where the file of ``spec`` lives once downloaded."""
        return self._model_dir / "detail" / spec.model_id / spec.file_name

    def ensure(self, spec: ModelSpec) -> Path:
        """Return the verified file of ``spec``, downloading it first when needed.

        Raises:
            DetailModelUnavailableError: Download failed or the file failed verification.
        """
        target = self.path_of(spec)
        if target.is_file() and sha256_of(target) == spec.sha256:
            return target
        target.unlink(missing_ok=True)
        target.parent.mkdir(parents=True, exist_ok=True)
        part = target.with_name(target.name + ".part")
        if self._download(spec.url, part) != spec.sha256:
            part.unlink(missing_ok=True)
            raise DetailModelUnavailableError(DOWNLOAD_CORRUPT)
        part.replace(target)
        return target

    def _download(self, url: str, part: Path) -> str:
        """Stream ``url`` into ``part`` and return its SHA-256; failures remove ``part``."""
        try:
            return self._fetch(url, part)
        except (OSError, HTTPException) as exc:
            part.unlink(missing_ok=True)
            raise DetailModelUnavailableError(DOWNLOAD_FAILED) from exc

    def _fetch(self, url: str, part: Path) -> str:
        digest = hashlib.sha256()
        with self._opener(url) as source, part.open("wb") as out:
            while chunk := source.read(_CHUNK):
                digest.update(chunk)
                out.write(chunk)
        return digest.hexdigest()
