"""``UploadIntake`` implementation: extension check, size sanity check and content hashing.

The client-supplied file name only provides the title and the extension; it is never used to
build or open a path. The source key is derived from the stored bytes.
"""

import hashlib
import re
from pathlib import Path
from typing import Final

from stemstudio_worker.ports import (
    EmptyUploadError,
    SourceInfo,
    UnsupportedMediaError,
    UploadTooLargeError,
)
from stemstudio_worker.schemas import SourceType

ALLOWED_EXTENSIONS: Final = frozenset({"mp3", "wav", "flac"})
"""Accepted upload extensions (compared case-insensitively)."""

DEFAULT_MAX_UPLOAD_BYTES: Final = 200 * 1024 * 1024
"""200 MiB (209,715,200 bytes); production uses ``WorkerSettings.max_upload_bytes``."""

FALLBACK_TITLE: Final = "Untitled upload"
"""Title used when the file name has no usable base name."""

MAX_TITLE_LENGTH: Final = 500
"""Longest title kept (``SourceMetadata`` limit)."""

_UNSUPPORTED_MESSAGE: Final = "Only MP3, WAV and FLAC files are supported."
_EMPTY_MESSAGE: Final = "The uploaded file is empty."
_CONTROL_CHARS: Final = re.compile(r"[\x00-\x1f\x7f]")


class FileUploadIntake:
    """Validates uploads stored by the API and derives ``upload:<sha256>`` source keys."""

    def __init__(self, max_upload_bytes: int = DEFAULT_MAX_UPLOAD_BYTES) -> None:
        """Create the intake.

        Args:
            max_upload_bytes: Largest accepted file; the API enforces it while streaming (413),
                this is a second line of defence.
        """
        self.max_upload_bytes = max_upload_bytes

    def accept(self, file_name: str, path: Path) -> SourceInfo:
        """Validate an upload (see ``ports.UploadIntake.accept``).

        Raises:
            UnsupportedMediaError: The extension is not MP3, WAV or FLAC (HTTP 415).
            EmptyUploadError: The stored file is empty (HTTP 400).
            UploadTooLargeError: The stored file is over the limit (HTTP 413).
        """
        base, extension = _split_name(file_name)
        if extension.lower() not in ALLOWED_EXTENSIONS:
            raise UnsupportedMediaError(_UNSUPPORTED_MESSAGE)
        self._check_size(path.stat().st_size)
        return SourceInfo(
            source_type=SourceType.UPLOAD,
            source_key=f"upload:{_sha256(path)}",
            title=_title(base),
            artist=None,
            youtube_url=None,
        )

    def _check_size(self, size: int) -> None:
        """Reject empty files and files over ``max_upload_bytes``."""
        if size == 0:
            raise EmptyUploadError(_EMPTY_MESSAGE)
        if size > self.max_upload_bytes:
            limit_mib = f"{self.max_upload_bytes / (1024 * 1024):g}"
            msg = f"The file is larger than {limit_mib} MiB."
            raise UploadTooLargeError(msg)


def _split_name(file_name: str) -> tuple[str, str]:
    """Return (base name without extension, extension) of the last path segment."""
    last = re.split(r"[\\/]", file_name)[-1]
    base, dot, extension = last.rpartition(".")
    return (base, extension) if dot else (last, "")


def _title(base: str) -> str:
    """Title from the base name: control characters removed, trimmed, at most 500 chars."""
    title = _CONTROL_CHARS.sub("", base).strip()[:MAX_TITLE_LENGTH].strip()
    return title or FALLBACK_TITLE


def _sha256(path: Path) -> str:
    """Lower-case hex SHA-256 of the stored bytes."""
    with path.open("rb") as handle:
        return hashlib.file_digest(handle, "sha256").hexdigest()
