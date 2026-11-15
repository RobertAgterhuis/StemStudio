"""Behaviour every fixture generator shares: determinism, committed manifest, command line."""

from __future__ import annotations

import hashlib
import json
import runpy
import sys
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Any

import pytest

import generate_artwork_fixtures
import generate_detail_fixtures
import generate_fixtures
import generate_score_fixtures
import generate_voice_fixtures

FIXTURE_ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class Spec:
    """A generator, its committed manifest, one file it writes and its summary text."""

    generator: ModuleType
    manifest: str
    sample: str
    summary: str


GENERATORS = [
    pytest.param(
        Spec(generate_fixtures, "manifest.json", "music_100bpm_20s.mp3", "8 fixtures"), id="001"
    ),
    pytest.param(
        Spec(
            generate_detail_fixtures,
            "detail-manifest.json",
            "detail_10s_band.wav",
            "13 detail fixtures",
        ),
        id="detail",
    ),
    pytest.param(
        Spec(
            generate_artwork_fixtures,
            "artwork-manifest.json",
            "cover_embedded_10s.mp3",
            "6 artwork fixtures",
        ),
        id="artwork",
    ),
    pytest.param(
        Spec(
            generate_score_fixtures,
            "score-manifest.json",
            "score_drums_kick.wav",
            "7 score fixtures",
        ),
        id="score",
    ),
    pytest.param(
        Spec(
            generate_voice_fixtures,
            "voice-manifest.json",
            "own-voice-20s.wav",
            "5 voice fixtures",
        ),
        id="voice",
    ),
]


def _sha256_tree(directory: Path) -> dict[str, str]:
    return {
        p.name: hashlib.sha256(p.read_bytes()).hexdigest()
        for p in sorted(directory.iterdir())
        if p.is_file()
    }


def _generate(generator: ModuleType, out_dir: Path) -> dict[str, Any]:
    generator.generate(out_dir, out_dir / "manifest.json")
    manifest: dict[str, Any] = json.loads((out_dir / "manifest.json").read_text("utf-8"))
    return manifest


@pytest.mark.parametrize("spec", GENERATORS)
def test_rerun_is_byte_identical(spec: Spec, tmp_path: Path) -> None:
    _generate(spec.generator, tmp_path / "a")
    _generate(spec.generator, tmp_path / "b")
    assert _sha256_tree(tmp_path / "a") == _sha256_tree(tmp_path / "b")


@pytest.mark.parametrize("spec", GENERATORS)
def test_every_manifest_entry_is_written(spec: Spec, tmp_path: Path) -> None:
    manifest = _generate(spec.generator, tmp_path)
    written = {p.name for p in tmp_path.iterdir()} - {"manifest.json"}
    assert written == set(manifest["fixtures"])
    assert manifest["sampleRate"] == 44_100


@pytest.mark.parametrize("spec", GENERATORS)
def test_committed_manifest_is_up_to_date(spec: Spec, tmp_path: Path) -> None:
    committed = json.loads((FIXTURE_ROOT / spec.manifest).read_text(encoding="utf-8"))
    assert committed == _generate(spec.generator, tmp_path)


@pytest.mark.parametrize("spec", GENERATORS)
def test_main_writes_to_the_given_paths(
    spec: Spec, tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    target = tmp_path / spec.manifest
    assert spec.generator.main(["--out-dir", str(tmp_path / "out"), "--manifest", str(target)]) == 0
    assert target.is_file()
    assert (tmp_path / "out" / spec.sample).is_file()
    assert spec.summary in capsys.readouterr().out


@pytest.mark.parametrize("spec", GENERATORS)
def test_main_defaults_point_into_the_fixture_folder(spec: Spec) -> None:
    args = spec.generator.parse_args([])
    assert args.out_dir == FIXTURE_ROOT / "audio"
    assert args.manifest == FIXTURE_ROOT / spec.manifest


@pytest.mark.parametrize("spec", GENERATORS)
def test_running_the_script_exits_with_zero(
    spec: Spec, monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    script = Path(spec.generator.__file__ or "")
    argv = [script.name, "--out-dir", str(tmp_path), "--manifest", str(tmp_path / spec.manifest)]
    monkeypatch.setattr(sys, "argv", argv)
    with pytest.raises(SystemExit) as exit_info:
        runpy.run_path(str(script), run_name="__main__")
    assert exit_info.value.code == 0
