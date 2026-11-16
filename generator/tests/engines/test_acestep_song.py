"""ACE-Step song path: text2music, LM planner in bf16 with the pt backend, profiles (010 T022)."""

from pathlib import Path
from typing import Any

import pytest

from stemstudio_generator.engines.acestep_engine import LM_MODEL, AceStepEngine
from stemstudio_generator.ports import (
    COVER_TURBO,
    SONG_DIRECT,
    SONG_DIRECT_LOWMEM,
    SONG_PLANNED,
    EngineSong,
    GeneratorError,
    GpuNeed,
    GpuOutOfMemoryError,
)
from stemstudio_generator.schemas import PLANNING_STAGE, SongParams

from .acestep_fake import Recorder
from .test_acestep_engine import _engine, _use

SONG = SongParams(
    caption="Pop rock, female lead vocal",
    lyrics="[verse]\nSing it loud\n\n[chorus]\nThis song is mine",
    instrumental=False,
    vocal_language="en",
    duration_seconds=30.0,
    seeds=[7],
)


def _song(tmp_path: Path, params: SongParams = SONG, *, planned: bool = True) -> EngineSong:
    out = tmp_path / "work"
    out.mkdir(exist_ok=True)
    return EngineSong(params=params, planned=planned, out_dir=out)


def _run(
    tmp_path: Path, need: GpuNeed, params: SongParams = SONG, recorder: Recorder | None = None
) -> tuple[Recorder, list[Any], list[tuple[float, str]]]:
    recorder = recorder or Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(need), lambda fraction, stage: None)
    seen: list[tuple[float, str]] = []
    takes = engine.song(_song(tmp_path, params), lambda f, s: seen.append((f, s)))
    return recorder, takes, seen


def test_a_planned_song_runs_text2music_with_the_lm(tmp_path: Path) -> None:
    recorder, takes, _ = _run(tmp_path, SONG_PLANNED)

    p, c, _ = recorder.calls[0]
    assert p.values["task_type"] == "text2music"
    assert (p.values["caption"], p.values["lyrics"]) == (SONG.caption, SONG.lyrics)
    assert (p.values["instrumental"], p.values["vocal_language"]) == (False, "en")
    assert (p.values["duration"], p.values["inference_steps"], p.values["shift"]) == (30.0, 8, 3.0)
    assert p.values["thinking"] is True
    assert (p.values["bpm"], p.values["keyscale"], p.values["timesignature"]) == (None, "", "")
    assert p.values["use_cot_metas"] is True
    assert p.values["use_cot_caption"] is False
    assert c.values["seeds"] == [7]
    assert [take.profile for take in takes] == ["planned"]


def test_the_lm_is_loaded_with_the_pt_backend_directly_in_bf16(tmp_path: Path) -> None:
    recorder, _, _ = _run(tmp_path, SONG_PLANNED)

    [init] = recorder.llm_inits
    assert init["lm_model_path"] == LM_MODEL
    assert (init["backend"], init["device"], init["offload_to_cpu"]) == ("pt", "cuda", False)
    assert init["dtype"] == "bfloat16"
    assert init["checkpoint_dir"].endswith("checkpoints")
    assert recorder.pretrained == [{"trust_remote_code": True, "torch_dtype": "bfloat16"}]
    assert recorder.moved == ["cuda"]


def test_a_failing_lm_load_is_reported(tmp_path: Path) -> None:
    recorder = Recorder(llm_result=("CUDA out of memory while loading", False))
    engine = _engine(tmp_path, recorder)

    with pytest.raises(GpuOutOfMemoryError):
        engine.load(_use(SONG_PLANNED), lambda fraction, stage: None)


def test_the_bf16_loader_reports_its_own_failure(tmp_path: Path) -> None:
    recorder = Recorder(pretrained_error=OSError("missing shard"))
    engine = _engine(tmp_path, recorder)

    with pytest.raises(GeneratorError, match="could not be loaded"):
        engine.load(_use(SONG_PLANNED), lambda fraction, stage: None)


def test_planning_progress_says_planning_song_structure(tmp_path: Path) -> None:
    _, _, seen = _run(tmp_path, SONG_PLANNED)

    assert seen == [(0.3, PLANNING_STAGE), (0.9, "Generating take 1 of 1")]


@pytest.mark.parametrize("need", [SONG_DIRECT, SONG_DIRECT_LOWMEM])
def test_direct_songs_run_the_dit_alone_with_defaults(tmp_path: Path, need: GpuNeed) -> None:
    recorder, takes, seen = _run(tmp_path, need)

    p = recorder.calls[0][0]
    assert p.values["thinking"] is False
    assert (p.values["bpm"], p.values["keyscale"], p.values["timesignature"]) == (120, "", "4")
    assert p.values["use_cot_metas"] is False
    assert recorder.llm_inits == []
    assert [take.profile for take in takes] == ["direct"]
    assert all(stage != PLANNING_STAGE for _, stage in seen)
    assert recorder.inits[0]["offload_to_cpu"] is (need is SONG_DIRECT_LOWMEM)


def test_direct_songs_take_bpm_key_and_time_signature_from_the_request(tmp_path: Path) -> None:
    params = SONG.model_copy(update={"bpm": 96, "key_scale": "F# minor", "time_signature": "3"})

    recorder, _, _ = _run(tmp_path, SONG_DIRECT, params)

    p = recorder.calls[0][0]
    assert (p.values["bpm"], p.values["keyscale"], p.values["timesignature"]) == (
        96,
        "F# minor",
        "3",
    )


def test_a_song_not_asking_for_planning_is_direct_even_with_the_lm_loaded(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)
    engine.load(_use(SONG_PLANNED), lambda fraction, stage: None)

    takes = engine.song(_song(tmp_path, planned=False), lambda fraction, stage: None)

    assert recorder.calls[0][0].values["thinking"] is False
    assert [take.profile for take in takes] == ["direct"]


def test_switching_profiles_keeps_the_dit_and_loads_or_drops_the_lm(tmp_path: Path) -> None:
    recorder = Recorder()
    engine = _engine(tmp_path, recorder)

    engine.load(_use(COVER_TURBO), lambda fraction, stage: None)
    engine.load(_use(SONG_PLANNED), lambda fraction, stage: None)
    engine.load(_use(SONG_DIRECT), lambda fraction, stage: None)

    assert len(recorder.inits) == 1
    assert len(recorder.llm_inits) == 1
    assert recorder.llm_unloads == 1


def test_unload_releases_the_lm_with_the_dit(tmp_path: Path) -> None:
    recorder = Recorder()
    engine: AceStepEngine = _engine(tmp_path, recorder)
    engine.load(_use(SONG_PLANNED), lambda fraction, stage: None)

    engine.unload()

    assert recorder.llm_unloads == 1


def test_an_instrumental_is_passed_through(tmp_path: Path) -> None:
    params = SONG.model_copy(update={"instrumental": True, "lyrics": "[Instrumental]"})

    recorder, _, _ = _run(tmp_path, SONG_DIRECT, params)

    assert recorder.calls[0][0].values["instrumental"] is True
    assert recorder.calls[0][0].values["lyrics"] == "[Instrumental]"
