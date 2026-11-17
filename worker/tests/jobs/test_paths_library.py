"""``DataPaths.library_stem`` (feature 013): library paths built from the UUID and the stem enum."""

from pathlib import Path
from uuid import UUID

from stemstudio_worker.jobs.paths import DataPaths

_TRACK = UUID("0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f")


def test_library_stem_is_below_tracks_uuid_stems(tmp_path: Path) -> None:
    paths = DataPaths(tmp_path / "data", library=tmp_path / "library")

    path = paths.library_stem(_TRACK, "piano")

    assert path == tmp_path / "library" / "tracks" / str(_TRACK) / "stems" / "piano.flac"


def test_library_stem_without_a_library_is_none(tmp_path: Path) -> None:
    assert DataPaths(tmp_path).library_stem(_TRACK, "vocals") is None


def test_rehydrate_staging_is_a_partial_folder_of_the_audio_cache(tmp_path: Path) -> None:
    paths = DataPaths(tmp_path)

    assert paths.rehydrate_staging("1" * 32) == paths.audio_cache / f"{'1' * 32}.partial"
