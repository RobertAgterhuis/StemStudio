"""Engine load profiles per ``GpuNeed``, the model catalogue and the take labels.

The ``GpuNeed`` constants are measured values in ``ports.py`` (009 research.md S-03, 010
S-04/S-05/S-07). This module says what an engine does with the need the gate granted: whether
DiT, VAE and text encoder are offloaded to the CPU (``lowMemory``), whether the 1.7B language
model is loaded (only ``SONG_PLANNED``, ADR-018 D6) and which song profile a take reports.
"""

from collections.abc import Callable, Sequence
from dataclasses import dataclass
from typing import Final

from stemstudio_generator.ports import (
    ACESTEP_WEIGHTS,
    COVER_TURBO,
    COVER_TURBO_LOWMEM,
    RMVPE_WEIGHTS,
    SONG_DIRECT,
    SONG_DIRECT_LOWMEM,
    SONG_NEEDS,
    SONG_PLANNED,
    SOULX_SVC_WEIGHTS,
    SVC_FP16,
    WHISPER_BASE_WEIGHTS,
    GpuNeed,
)
from stemstudio_generator.schemas import ModelId, ModelStatus, SongProfile, TakeModel


@dataclass(frozen=True, slots=True)
class LoadProfile:
    """How an engine loads for one ``GpuNeed``.

    Attributes:
        need: The granted need.
        cpu_offload: Offload DiT, VAE and text encoder to the CPU (``lowMemory``, about 4x slower).
        language_model: Load the 5Hz LM 1.7B planner (``thinking=True``; bf16 at load time).
        song_profile: Song profile a take of this need reports (None for non-song needs).
    """

    need: GpuNeed
    cpu_offload: bool
    language_model: bool
    song_profile: SongProfile | None


_PROFILES: Final[dict[str, LoadProfile]] = {
    profile.need.name: profile
    for profile in (
        # need                 cpu_offload  language_model  song_profile
        LoadProfile(COVER_TURBO,        False, False, None),
        LoadProfile(COVER_TURBO_LOWMEM, True,  False, None),
        LoadProfile(SONG_PLANNED,       False, True,  "planned"),
        LoadProfile(SONG_DIRECT,        False, False, "direct"),
        LoadProfile(SONG_DIRECT_LOWMEM, True,  False, "direct"),
        LoadProfile(SVC_FP16,           False, False, None),
    )
}  # fmt: skip


def load_profile(need: GpuNeed) -> LoadProfile:
    """The load profile of ``need``.

    Raises:
        ValueError: ``need`` is not one of the measured needs.
    """
    try:
        return _PROFILES[need.name]
    except KeyError:
        msg = f"No load profile for GPU need {need.name}"
        raise ValueError(msg) from None


def song_needs(*, thinking: bool) -> tuple[GpuNeed, ...]:
    """Ordered song needs: with the planner first when ``thinking``, else the DiT alone."""
    return SONG_NEEDS if thinking else tuple(n for n in SONG_NEEDS if n is not SONG_PLANNED)


def low_memory_needs(needs: Sequence[GpuNeed]) -> tuple[GpuNeed, ...]:
    """The ``lowMemory`` needs of ``needs``: the candidates of the one OOM retry (ADR-018 D4)."""
    return tuple(need for need in needs if need.profile == "lowMemory")


# ------------------------------------------------------------------ models


@dataclass(frozen=True, slots=True)
class ModelFile:
    """One model of an engine as ``/health`` lists it.

    Attributes:
        id: Contract model id.
        bytes: Download size.
        revision: Pinned Hugging Face revision.
    """

    id: ModelId
    bytes: int
    revision: str


ACESTEP_MODELS: Final[tuple[ModelFile, ...]] = (
    ModelFile("acestep-v15-turbo", 4_790_000_000, ACESTEP_WEIGHTS.revision),
    ModelFile("acestep-vae", 340_000_000, ACESTEP_WEIGHTS.revision),
    ModelFile("qwen3-embedding-0.6b", 1_190_000_000, ACESTEP_WEIGHTS.revision),
    ModelFile("acestep-5hz-lm-1.7b", 3_710_000_000, ACESTEP_WEIGHTS.revision),
)
"""ACE-Step 1.5 bundle (one revision), in the order of the contract examples."""

SOULX_MODELS: Final[tuple[ModelFile, ...]] = (
    ModelFile("soulx-singer-svc", 2_790_000_000, SOULX_SVC_WEIGHTS.revision),
    ModelFile("rmvpe", 180_000_000, RMVPE_WEIGHTS.revision),
    ModelFile("whisper-base", 290_000_000, WHISPER_BASE_WEIGHTS.revision),
)
"""Singer models (1.1.0)."""


def model_statuses(
    models: Sequence[ModelFile], present: Callable[[ModelId], bool]
) -> list[ModelStatus]:
    """``ModelStatus`` of each model, ``present`` decided per model id."""
    return [
        ModelStatus(id=m.id, present=present(m.id), bytes=m.bytes, revision=m.revision)
        for m in models
    ]


# ------------------------------------------------------------------ take labels

MUSIC_TAKE_MODEL: Final = TakeModel(id="acestep-v15-turbo", revision=ACESTEP_WEIGHTS.revision)
"""``GeneratedTake.model`` of restyle and song takes."""

VOICE_TAKE_MODEL: Final = TakeModel(id="soulx-singer-svc", revision=SOULX_SVC_WEIGHTS.revision)
"""``GeneratedTake.model`` of voice-conversion takes (1.1.0)."""

MUSIC_GENERATOR_LABEL: Final = f"ACE-Step 1.5 ({ACESTEP_WEIGHTS.revision})"
"""``GENERATOR`` Vorbis comment of restyle and song takes."""

VOICE_GENERATOR_LABEL: Final = f"SoulX-Singer-SVC ({SOULX_SVC_WEIGHTS.revision})"
"""``GENERATOR`` Vorbis comment of voice-conversion takes."""
