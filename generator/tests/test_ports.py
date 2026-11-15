"""Tests for the port value types, GPU needs and errors (009 + 010 Phase 0)."""

import numpy as np
import pytest

from stemstudio_generator.ports import (
    CONVERSION_NEEDS,
    COVER_TURBO,
    COVER_TURBO_LOWMEM,
    RESTYLE_NEEDS,
    SONG_DIRECT,
    SONG_DIRECT_LOWMEM,
    SONG_NEEDS,
    SONG_PLANNED,
    SVC_FP16,
    GeneratorError,
    GpuBusyError,
    GpuOutOfMemoryError,
    GpuRequiredError,
    InvalidInputError,
    ModelDownloadError,
    PromptInvalidError,
    UnvoicedTargetError,
    voiced_ratio,
)
from stemstudio_generator.schemas import OUT_OF_MEMORY_TEXT


def test_needs_are_ordered_by_preference_and_fall_back_to_low_memory() -> None:
    assert RESTYLE_NEEDS == (COVER_TURBO, COVER_TURBO_LOWMEM)
    assert SONG_NEEDS == (SONG_PLANNED, SONG_DIRECT, SONG_DIRECT_LOWMEM)
    assert CONVERSION_NEEDS == (SVC_FP16,)
    for needs in (RESTYLE_NEEDS, SONG_NEEDS[1:]):
        assert [n.memory_mib for n in needs] == sorted((n.memory_mib for n in needs), reverse=True)
        assert needs[-1].profile == "lowMemory"


def test_needs_are_the_measured_values_in_mib() -> None:
    measured = {n.name: n.memory_mib for n in (*RESTYLE_NEEDS, *SONG_NEEDS, *CONVERSION_NEEDS)}

    assert measured == {
        "COVER_TURBO": 13_926, "COVER_TURBO_LOWMEM": 8_602, "SONG_PLANNED": 12_288,
        "SONG_DIRECT": 9_830, "SONG_DIRECT_LOWMEM": 7_578, "SVC_FP16": 6_144,
    }  # fmt: skip


@pytest.mark.parametrize(
    ("error", "code"),
    [
        (GeneratorError("x"), "engineError"),
        (GpuOutOfMemoryError(), "outOfMemory"),
        (GpuBusyError("x"), "gpuBusy"),
        (GpuRequiredError("x"), "gpuRequired"),
        (ModelDownloadError("x"), "modelDownloadFailed"),
        (InvalidInputError("x"), "invalidInput"),
        (UnvoicedTargetError("x"), "unvoicedTarget"),
        (PromptInvalidError("x"), "promptInvalid"),
    ],
)
def test_errors_carry_their_contract_code(error: GeneratorError, code: str) -> None:
    assert error.code == code


def test_out_of_memory_has_the_readable_default_text() -> None:
    assert str(GpuOutOfMemoryError()) == OUT_OF_MEMORY_TEXT


def test_voiced_ratio_counts_frames_above_zero() -> None:
    assert voiced_ratio(np.array([0, 110, 220, 0], dtype=np.float32)) == 0.5
    assert voiced_ratio(np.array([], dtype=np.float32)) == 0.0
