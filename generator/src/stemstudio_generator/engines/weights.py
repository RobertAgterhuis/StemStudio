"""Pinned model weights in the shared ``model-cache`` volume (ADR-009 rule 1, ADR-018 D5).

Every weights directory carries a ``REVISION`` file (the pinned Hugging Face revision; one line,
or one ``<file> <repo> <revision>`` line per file) and a ``SHA256SUMS`` file in ``sha256sum``
format. A directory counts as present when the revision matches the pin and the manifest lists
the pinned SHA-256 of every pinned file; ``verify`` hashes the pinned files once per process.
Missing weights are downloaded on first use, file by file at the pinned revision, with byte
progress (``downloadingModel``).
"""

import hashlib
import logging
from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path
from typing import Final

from stemstudio_generator.ports import ModelDownloadError, ProgressCallback, WeightsPin

DOWNLOAD_STAGE: Final = "Downloading model ({done:.1f} of {total:.1f} GB)"
"""Stage text while weights are downloaded (``str.format(done=, total=)`` in GB)."""

DOWNLOAD_FAILED_TEXT: Final = (
    "The model files could not be downloaded; check the internet connection and try again."
)
CHECKSUM_TEXT: Final = (
    "The model files failed their checksum; delete the model cache folder and try again."
)

REVISION_FILE: Final = "REVISION"
SUMS_FILE: Final = "SHA256SUMS"
_MANIFESTS: Final = frozenset({REVISION_FILE, SUMS_FILE})
_CHUNK: Final = 1 << 20
_BYTES_PER_GB: Final = 1e9
_LOG: Final = logging.getLogger(__name__)

Fetch = Callable[[str, str, str, Path], None]
"""``(repo, revision, filename, dest_dir)``: download one file into ``dest_dir``."""

Listing = Callable[[str, str], list[tuple[str, int]]]
"""``(repo, revision)``: the repository's files with their sizes."""


def hub_listing(repo: str, revision: str) -> list[tuple[str, int]]:
    """Files and sizes of ``repo`` at ``revision`` (``huggingface_hub``, anonymous)."""
    from huggingface_hub import HfApi  # type: ignore[import-not-found]  # noqa: PLC0415

    info = HfApi().model_info(repo, revision=revision, files_metadata=True)
    return [(sibling.rfilename, int(sibling.size or 0)) for sibling in info.siblings or []]


def hub_fetch(repo: str, revision: str, filename: str, dest: Path) -> None:
    """Download ``filename`` of ``repo`` at ``revision`` into ``dest``."""
    from huggingface_hub import hf_hub_download  # noqa: PLC0415 - GPU image variants only

    hf_hub_download(repo_id=repo, revision=revision, filename=filename, local_dir=str(dest))


@dataclass(frozen=True, slots=True)
class Hub:
    """Where missing weights come from.

    Attributes:
        fetch: Downloads one file.
        listing: Lists the files of a revision.
    """

    fetch: Fetch = hub_fetch
    listing: Listing = hub_listing


def file_sha256(path: Path) -> str:
    """SHA-256 of a file, streamed."""
    digest = hashlib.sha256()
    with path.open("rb") as source:
        while chunk := source.read(_CHUNK):
            digest.update(chunk)
    return digest.hexdigest()


def write_manifest(root: Path, revision: str | None) -> None:
    """Write ``SHA256SUMS`` (every file, hub cache folders excluded) and ``REVISION``.

    ``revision`` None keeps ``REVISION`` (directories shared by several pins).
    """
    files = sorted(path for path in root.rglob("*") if _is_weight_file(path))
    lines = [f"{file_sha256(path)}  {path.relative_to(root).as_posix()}" for path in files]
    (root / SUMS_FILE).write_text("\n".join(lines) + "\n", "utf-8")
    if revision is not None:
        (root / REVISION_FILE).write_text(f"{revision}\n", "utf-8")


def _is_weight_file(path: Path) -> bool:
    """A file of the weights (not a manifest, not the hub's ``.cache`` folder)."""
    return path.is_file() and path.name not in _MANIFESTS and ".cache" not in path.parts


def _lines(path: Path) -> list[str]:
    """Non-blank lines of ``path``; none when it does not exist."""
    if not path.is_file():
        return []
    return [line for line in path.read_text("utf-8").splitlines() if line.strip()]


@dataclass
class WeightsDir:
    """One pinned weights directory.

    Attributes:
        root: The directory.
        pin: Repository, revision and pinned files.
        hub: Download source.
        only_pinned: The directory holds only the pinned files of this repository (possibly
            next to files of other pins, ``soulx/``): download just those and record their
            revision per file (``<file> <repo> <revision>`` lines).
    """

    root: Path
    pin: WeightsPin
    hub: Hub = field(default_factory=Hub)
    only_pinned: bool = False
    _verified: bool = field(default=False, init=False)

    def present(self) -> bool:
        """Revision matches and every pinned file is listed with its pinned hash."""
        names = [name for name, _ in self.pin.files]
        sums = self._sums()
        return self._revision_ok(names) and all(self._file_ok(name, sums) for name in names)

    def file_present(self, name: str) -> bool:
        """``name`` (relative path) is in the pinned cache."""
        return self._revision_ok([name]) and self._file_ok(name, self._sums())

    def verify(self) -> None:
        """Hash the pinned files once per process (``ModelDownloadError`` on a mismatch)."""
        if self._verified:
            return
        for name, expected in self.pin.files:
            if expected and file_sha256(self.root / name) != expected:
                _LOG.error("Checksum mismatch of %s in %s", name, self.root)
                raise ModelDownloadError(CHECKSUM_TEXT)
        self._verified = True

    def ensure(self, on_progress: ProgressCallback) -> None:
        """Download the pinned revision when it is not present, then write the manifest."""
        if self.present():
            return
        try:
            self._download(on_progress)
        except (OSError, RuntimeError, ValueError) as error:
            _LOG.warning("Download of %s failed: %s", self.pin.repo, error)
            raise ModelDownloadError(DOWNLOAD_FAILED_TEXT) from error
        write_manifest(self.root, None if self.only_pinned else self.pin.revision)
        if self.only_pinned:
            self._record_revisions()
        if not self.present():
            raise ModelDownloadError(CHECKSUM_TEXT)

    def _download(self, on_progress: ProgressCallback) -> None:
        files = (
            [(name, 1) for name, _ in self.pin.files]
            if self.only_pinned
            else self.hub.listing(self.pin.repo, self.pin.revision)
        )
        total = sum(size for _, size in files) or 1
        done = 0
        self.root.mkdir(parents=True, exist_ok=True)
        for name, size in files:
            on_progress(done / total, _stage(done, total))
            self.hub.fetch(self.pin.repo, self.pin.revision, name, self.root)
            done += size
        on_progress(1.0, _stage(done, total))

    def _revision_ok(self, names: list[str]) -> bool:
        """One revision for the directory, or the pinned revision on each file's line."""
        lines = [line.split() for line in _lines(self.root / REVISION_FILE)]
        whole = {parts[0] for parts in lines if len(parts) == 1}
        if whole:
            return whole == {self.pin.revision}
        by_file = {parts[0]: parts[-1] for parts in lines}
        return bool(names) and all(by_file.get(name) == self.pin.revision for name in names)

    def _record_revisions(self) -> None:
        """Write this pin's files into the per-file ``REVISION`` (other pins' lines stay)."""
        lines = [line.split() for line in _lines(self.root / REVISION_FILE)]
        by_file = {parts[0]: " ".join(parts) for parts in lines if len(parts) > 1}
        for name, _ in self.pin.files:
            by_file[name] = f"{name} {self.pin.repo} {self.pin.revision}"
        text = "\n".join(by_file[name] for name in sorted(by_file)) + "\n"
        (self.root / REVISION_FILE).write_text(text, "utf-8")

    def _sums(self) -> dict[str, str]:
        pairs = (line.split(maxsplit=1) for line in _lines(self.root / SUMS_FILE))
        return {pair[-1].strip(): pair[0] for pair in pairs}

    def _file_ok(self, name: str, sums: dict[str, str]) -> bool:
        expected = dict(self.pin.files).get(name, "")
        listed = sums.get(name)
        hash_ok = listed is not None and (not expected or listed == expected)
        return hash_ok and (self.root / name).is_file()


def _stage(done: int, total: int) -> str:
    return DOWNLOAD_STAGE.format(done=done / _BYTES_PER_GB, total=total / _BYTES_PER_GB)
