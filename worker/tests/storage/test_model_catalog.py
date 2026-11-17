"""Tests of ``storage.model_catalog`` (T025): known model ids per model-cache folder."""

from pathlib import Path

import pytest

from stemstudio_worker.detail.registry import MODELS
from stemstudio_worker.ports import BASIC_PITCH_WEIGHTS, PIANO_WEIGHTS, WHISPER_TURBO_WEIGHTS
from stemstudio_worker.storage.model_catalog import CATALOG, models_in

from .builders import write


def test_detail_entries_match_the_detail_registry() -> None:
    detail = {(e.model_id, e.marker) for e in CATALOG if e.folder == "detail"}

    assert detail == {(m.model_id, f"{m.model_id}/{m.file_name}") for m in MODELS}


def test_weights_entries_match_the_pinned_weights() -> None:
    ids = {(e.folder, e.model_id, e.marker) for e in CATALOG}

    assert ("whisper", "whisper-large-v3-turbo", f"{WHISPER_TURBO_WEIGHTS.model_id}.pt") in ids
    for weights in (BASIC_PITCH_WEIGHTS, PIANO_WEIGHTS):
        assert ("transcription", weights.model_id, weights.model_id) in ids


@pytest.mark.parametrize(
    ("folder", "marker", "model_id"),
    [
        ("torch", "hub/checkpoints/5c90dfd2-34c22ccb.th", "htdemucs_6s"),
        ("hf", "hub/models--adefossez--HTDemucs/refs/main", "htdemucs"),
        ("whisper", "small.pt", "whisper-small"),
    ],
)
def test_a_present_marker_reports_its_model(
    tmp_path: Path, folder: str, marker: str, model_id: str
) -> None:
    write(tmp_path / folder / marker, 1)

    assert model_id in models_in(tmp_path / folder)


def test_unknown_folders_and_missing_markers_report_nothing(tmp_path: Path) -> None:
    (tmp_path / "torch").mkdir()

    assert models_in(tmp_path / "torch") == []
    assert models_in(tmp_path / "acestep") == []
