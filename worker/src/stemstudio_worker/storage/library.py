"""``LibraryStemReader`` over the read-only API library mount (``STEMSTUDIO_LIBRARY_DIR``, 013)."""

from pathlib import Path
from typing import BinaryIO
from uuid import UUID

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.ports import LibraryUnavailableError, StemMissingError
from stemstudio_worker.schemas import StemName


class FileLibraryStemReader:
    """Opens ``<library>/tracks/<uuid>/stems/<stem>.flac`` (``DataPaths.library_stem``)."""

    def __init__(self, paths: DataPaths) -> None:
        """Read below ``paths.library``."""
        self._paths = paths

    def open(self, track_id: UUID, stem: StemName) -> BinaryIO:
        """Open the stem for binary reading; the caller closes the stream.

        Raises:
            LibraryUnavailableError: No library is configured, it is not a folder, or the file
                cannot be read for another reason than not existing.
            StemMissingError: The stem file does not exist.
        """
        path = self._stem_path(track_id, stem)
        try:
            return path.open("rb")
        except FileNotFoundError as exc:
            raise StemMissingError(stem) from exc
        except OSError as exc:
            raise LibraryUnavailableError from exc

    def _stem_path(self, track_id: UUID, stem: StemName) -> Path:
        library, path = self._paths.library, self._paths.library_stem(track_id, stem)
        if library is None or path is None or not library.is_dir():
            raise LibraryUnavailableError
        return path
