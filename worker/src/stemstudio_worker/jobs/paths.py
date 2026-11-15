"""Layout of the worker data directory (data-model.md section 4).

Every path component is generated (hex job id, hex hash, enum stem name, sanitised source key);
user-supplied names are never used.
"""

from dataclasses import dataclass
from pathlib import Path

from stemstudio_worker.schemas import DetailStemName, StemName


@dataclass(frozen=True, slots=True)
class DataPaths:
    """Paths below ``STEMSTUDIO_DATA_DIR``.

    Attributes:
        root: The data directory.
    """

    root: Path

    @property
    def jobs(self) -> Path:
        """Directory of the ``<jobId>.json`` job files."""
        return self.root / "jobs"

    def work(self, job_id: str) -> Path:
        """Temporary download and normalised audio of a job; deleted when terminal."""
        return self.root / "work" / job_id

    def uploads(self, job_id: str) -> Path:
        """Received upload of a job (``input<ext>``); deleted when terminal."""
        return self.root / "uploads" / job_id

    def by_source(self, source_key: str) -> Path:
        """Cache pointer ``{"audioSha256": ...}`` for a source key (``:`` becomes ``_``)."""
        return self.root / "cache" / "by-source" / f"{source_key.replace(':', '_')}.json"

    @property
    def audio_cache(self) -> Path:
        """Directory of the per-audio cache entries."""
        return self.root / "cache" / "by-audio"

    def by_audio(self, audio_sha256: str) -> Path:
        """Cache entry (``result.json`` + ``stems/``) for normalised audio."""
        return self.audio_cache / audio_sha256

    def stem_file(self, audio_sha256: str, stem: StemName) -> Path:
        """FLAC file of one cached stem."""
        return self.by_audio(audio_sha256) / "stems" / f"{stem}.flac"

    @property
    def detail_jobs(self) -> Path:
        """Directory of the ``<detailJobId>.json`` detail job files."""
        return self.root / "detail-jobs"

    def detail_partial(self, audio_sha256: str) -> Path:
        """Per-parent checkpoints of an unfinished detail phase (kept across retries)."""
        return self.by_audio(audio_sha256) / "detail.partial"

    def detail_final(self, audio_sha256: str) -> Path:
        """Finished detail result (``result.json`` + ``<parent>/<name>.flac``)."""
        return self.by_audio(audio_sha256) / "detail"

    def detail_stem(self, audio_sha256: str, parent: StemName, name: DetailStemName) -> Path:
        """FLAC file of one cached detail stem."""
        return self.detail_final(audio_sha256) / parent / f"{name}.flac"
