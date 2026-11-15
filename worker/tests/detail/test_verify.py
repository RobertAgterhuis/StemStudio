"""Tests of the reconstruction check of stored detail stems (T082, quickstart D3, SC-002)."""

import runpy
import sys
from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.detail.audio_io import write_stem
from stemstudio_worker.detail.checks import from_pcm, rest_of, to_pcm
from stemstudio_worker.detail.verify import ParentCheck, check_track, format_report, main

from .signals import noise

SCRIPT = Path(__file__).resolve().parents[2] / "scripts" / "check_reconstruction.py"


def _track(root: Path, parent: str = "drums", rest_scale: float = 1.0) -> Path:
    """``root/stems/<parent>.flac`` plus ``root/detail/<parent>/{kick,snare,rest}.flac``."""
    audio = from_pcm(to_pcm(noise(0.5, 0.4)))
    (root / "stems").mkdir(parents=True, exist_ok=True)
    write_stem(root / "stems" / f"{parent}.flac", to_pcm(audio))
    kept = [to_pcm(audio * 0.6), to_pcm(audio * 0.3)]
    folder = root / "detail" / parent
    folder.mkdir(parents=True)
    write_stem(folder / "kick.flac", kept[0])
    write_stem(folder / "snare.flac", kept[1])
    write_stem(folder / "rest.flac", to_pcm(rest_of(audio, kept) * rest_scale))
    return root


def test_a_correct_split_reconstructs_the_parent(tmp_path: Path) -> None:
    (check,) = check_track(_track(tmp_path))
    assert check.parent == "drums"
    assert check.stems == ("kick", "snare", "rest")
    assert check.reconstruction_db <= -30
    assert check.inaudible == ()
    assert check.ok


def test_a_wrong_rest_fails(tmp_path: Path) -> None:
    (check,) = check_track(_track(tmp_path, rest_scale=8.0))
    assert check.reconstruction_db > -30
    assert not check.ok


def test_inaudible_or_too_few_stems_fail(tmp_path: Path) -> None:
    track = _track(tmp_path)
    quiet = np.zeros((2, 22050), dtype=np.float32)
    write_stem(track / "detail" / "drums" / "kick.flac", to_pcm(quiet))
    (check,) = check_track(track)
    assert check.inaudible == ("kick",)
    assert not check.ok
    assert not ParentCheck("vocals", ("lead", "rest"), -90.0, ()).ok
    assert not ParentCheck("vocals", ("lead", "backing"), -90.0, ()).ok


def test_parents_are_reported_in_standard_order(tmp_path: Path) -> None:
    _track(tmp_path, "other")
    _track(tmp_path, "vocals")
    assert [c.parent for c in check_track(tmp_path)] == ["vocals", "other"]


def test_report_and_exit_codes(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    good = _track(tmp_path / "good")
    bad = _track(tmp_path / "bad", rest_scale=8.0)
    assert main([str(good)]) == 0
    assert "drums" in capsys.readouterr().out
    assert main([str(bad)]) == 1
    assert "FAIL" in capsys.readouterr().out
    assert main([]) == 2
    assert main([str(tmp_path / "missing")]) == 2
    empty = tmp_path / "empty"
    empty.mkdir()
    assert main([str(empty)]) == 2
    assert "Usage" in capsys.readouterr().err


def test_report_lists_every_parent(tmp_path: Path) -> None:
    report = format_report(check_track(_track(tmp_path)))
    assert "drums" in report
    assert "kick, snare, rest" in report
    assert "OK" in report


def test_the_script_runs_the_check(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(sys, "argv", [str(SCRIPT), str(_track(tmp_path))])
    with pytest.raises(SystemExit) as raised:
        runpy.run_path(str(SCRIPT), run_name="__main__")
    assert raised.value.code == 0
