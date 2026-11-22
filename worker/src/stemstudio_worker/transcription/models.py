"""First-use downloads of the transcription weights (008 data-model 2.10, as the 002 model manager).

Weights live in ``STEMSTUDIO_MODEL_DIR/transcription/<model_id>/<file>``; a download goes to a
``.part`` file that is renamed only after its SHA-256 matched the pinned value, so a broken or
tampered file is never used. A download is aborted as soon as it exceeds the pinned size, and the
digest of a verified file is cached by size and modification time (``ready`` is asked by
``/health`` and every job).
"""

import hashlib
import shutil
import urllib.request
from collections.abc import Callable
from contextlib import AbstractContextManager
from pathlib import Path
from typing import IO, Final, cast

from stemstudio_worker.ports import TranscriptionError, WeightsFile

MODEL_DOWNLOAD_TEXT: Final = (
    "The transcription model could not be downloaded; try again when online."
)
"""Error of a failed or corrupt weights download."""

UrlOpener = Callable[[str], AbstractContextManager[IO[bytes]]]
"""Opens a pinned HTTPS URL (injectable for tests)."""

_TIMEOUT = 60.0
_BLOCK = 1 << 20


def https_open(url: str) -> AbstractContextManager[IO[bytes]]:
    """Open one of the pinned ``ports`` weight URLs."""
    return cast(
        AbstractContextManager[IO[bytes]],
        urllib.request.urlopen(url, timeout=_TIMEOUT),  # noqa: S310 - pinned HTTPS constants
    )


class _HashingWriter:
    """File-like sink that hashes what is written and refuses more than ``limit`` bytes."""

    def __init__(self, sink: IO[bytes], limit: int) -> None:
        self.digest = hashlib.sha256()
        self._sink = sink
        self._left = limit

    def write(self, block: bytes) -> int:
        self._left -= len(block)
        if self._left < 0:
            msg = "the download is larger than the pinned size"
            raise OSError(msg)
        self.digest.update(block)
        return self._sink.write(block)


def _file_digest(path: Path) -> str | None:
    try:
        with path.open("rb") as handle:
            return hashlib.file_digest(handle, "sha256").hexdigest()
    except OSError:
        return None


class ModelStore:
    """Hands out verified weight files, downloading them on first use."""

    def __init__(self, model_dir: Path, opener: UrlOpener = https_open) -> None:
        """Keep weights below ``model_dir/transcription``; fetch with ``opener``."""
        self._root = model_dir / "transcription"
        self._opener = opener
        self._verified: dict[Path, tuple[int, int, str]] = {}

    def path(self, weights: WeightsFile) -> Path:
        """Where ``weights`` live once downloaded."""
        return self._root / weights.model_id / weights.url.rsplit("/", 1)[-1]

    def ready(self, weights: WeightsFile) -> bool:
        """Whether a verified copy is in the cache (``Health.transcription.models``)."""
        return self._digest(self.path(weights)) == weights.sha256

    def _digest(self, path: Path) -> str | None:
        """The SHA-256 of ``path``, cached while its size and modification time stay the same."""
        try:
            stat = path.stat()
        except OSError:
            return None
        cached = self._verified.get(path)
        if cached is not None and cached[:2] == (stat.st_size, stat.st_mtime_ns):
            return cached[2]
        digest = _file_digest(path)
        if digest is not None:
            self._verified[path] = (stat.st_size, stat.st_mtime_ns, digest)
        return digest

    def ensure(self, weights: WeightsFile) -> Path:
        """The verified file of ``weights``.

        Raises:
            TranscriptionError: The download failed or did not match (``MODEL_DOWNLOAD_TEXT``).
        """
        target = self.path(weights)
        if self.ready(weights):
            return target
        partial = target.with_suffix(target.suffix + ".part")
        try:
            digest = self._fetch(weights, partial)
        except OSError as exc:
            digest = repr(exc)
        if digest != weights.sha256:
            partial.unlink(missing_ok=True)
            raise TranscriptionError(MODEL_DOWNLOAD_TEXT)
        partial.replace(target)
        self._verified.pop(target, None)
        return target

    def _fetch(self, weights: WeightsFile, partial: Path) -> str:
        partial.parent.mkdir(parents=True, exist_ok=True)
        with self._opener(weights.url) as source, partial.open("wb") as sink:
            writer = _HashingWriter(sink, weights.size_bytes)
            shutil.copyfileobj(source, writer, _BLOCK)
        return writer.digest.hexdigest()
