"""Tests for the engine load profiles, model catalogue and take labels (009 T020)."""

import pytest

from stemstudio_generator.engines.profiles import (
    ACESTEP_MODELS,
    MUSIC_GENERATOR_LABEL,
    MUSIC_TAKE_MODEL,
    SOULX_MODELS,
    VOICE_GENERATOR_LABEL,
    VOICE_TAKE_MODEL,
    load_profile,
    low_memory_needs,
    model_statuses,
    song_needs,
)
from stemstudio_generator.ports import (
    ACESTEP_WEIGHTS,
    CONVERSION_NEEDS,
    COVER_TURBO,
    COVER_TURBO_LOWMEM,
    RESTYLE_NEEDS,
    SONG_DIRECT,
    SONG_DIRECT_LOWMEM,
    SONG_NEEDS,
    SONG_PLANNED,
    SOULX_SVC_WEIGHTS,
    SVC_FP16,
    GpuNeed,
)


@pytest.mark.parametrize(
    ("need", "offload", "language_model", "song_profile"),
    [
        (COVER_TURBO, False, False, None),
        (COVER_TURBO_LOWMEM, True, False, None),
        (SONG_PLANNED, False, True, "planned"),
        (SONG_DIRECT, False, False, "direct"),
        (SONG_DIRECT_LOWMEM, True, False, "direct"),
        (SVC_FP16, False, False, None),
    ],
)
def test_every_need_has_a_load_profile(
    need: GpuNeed, offload: bool, language_model: bool, song_profile: str | None
) -> None:
    profile = load_profile(need)

    assert profile.need is need
    assert (profile.cpu_offload, profile.language_model) == (offload, language_model)
    assert profile.song_profile == song_profile
    assert profile.cpu_offload == (need.profile == "lowMemory")


def test_unknown_need_is_refused() -> None:
    with pytest.raises(ValueError, match="No load profile"):
        load_profile(GpuNeed("OTHER", 1))


def test_song_needs_skip_the_planner_without_thinking() -> None:
    assert song_needs(thinking=True) == SONG_NEEDS
    assert song_needs(thinking=False) == (SONG_DIRECT, SONG_DIRECT_LOWMEM)


def test_low_memory_needs_are_the_retry_candidates() -> None:
    assert low_memory_needs(RESTYLE_NEEDS) == (COVER_TURBO_LOWMEM,)
    assert low_memory_needs(SONG_NEEDS) == (SONG_DIRECT_LOWMEM,)
    assert low_memory_needs(CONVERSION_NEEDS) == ()


def test_model_catalogues_match_the_contract_ids_sizes_and_revisions() -> None:
    assert [(m.id, m.bytes) for m in ACESTEP_MODELS] == [
        ("acestep-v15-turbo", 4_790_000_000),
        ("acestep-vae", 340_000_000),
        ("qwen3-embedding-0.6b", 1_190_000_000),
        ("acestep-5hz-lm-1.7b", 3_710_000_000),
    ]
    assert {m.revision for m in ACESTEP_MODELS} == {ACESTEP_WEIGHTS.revision}
    assert [(m.id, m.bytes) for m in SOULX_MODELS] == [
        ("soulx-singer-svc", 2_790_000_000),
        ("rmvpe", 180_000_000),
        ("whisper-base", 290_000_000),
    ]
    assert SOULX_MODELS[0].revision == SOULX_SVC_WEIGHTS.revision


def test_model_statuses_report_presence_per_model() -> None:
    statuses = model_statuses(ACESTEP_MODELS, lambda model_id: model_id != "acestep-vae")

    assert [(s.id, s.present) for s in statuses] == [
        ("acestep-v15-turbo", True),
        ("acestep-vae", False),
        ("qwen3-embedding-0.6b", True),
        ("acestep-5hz-lm-1.7b", True),
    ]
    assert statuses[0].bytes == 4_790_000_000


def test_take_models_and_generator_labels() -> None:
    assert (MUSIC_TAKE_MODEL.id, MUSIC_TAKE_MODEL.revision) == (
        "acestep-v15-turbo",
        ACESTEP_WEIGHTS.revision,
    )
    assert (VOICE_TAKE_MODEL.id, VOICE_TAKE_MODEL.revision) == (
        "soulx-singer-svc",
        SOULX_SVC_WEIGHTS.revision,
    )
    assert f"ACE-Step 1.5 ({ACESTEP_WEIGHTS.revision})" == MUSIC_GENERATOR_LABEL
    assert f"SoulX-Singer-SVC ({SOULX_SVC_WEIGHTS.revision})" == VOICE_GENERATOR_LABEL
