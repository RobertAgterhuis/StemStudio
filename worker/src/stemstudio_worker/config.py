"""Worker configuration from environment variables (no secrets are needed or read)."""

import os
import re
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Literal, TypedDict, cast

SeparatorKind = Literal["demucs", "fake"]
DeviceKind = Literal["cpu", "cuda", "auto"]
FakeDeviceKind = Literal["cpu", "gpu", "gpu-oom"]
FakeOnlineMode = Literal["found", "not-found", "failed", "off"]
AlignModel = Literal["turbo", "small"]
AlignAutoMode = Literal["gpu", "always", "never"]
FakeLyricsMode = Literal["word", "line", "plain", "instrumental", "not-found", "failed"]
TranscriberKind = Literal["real", "fake"]
StructureAnalyzerKind = Literal["real", "fake"]
ChordEngineKind = Literal["btc", "chroma-hmm", "fake"]
PitchEngineKind = Literal["pyin", "yin", "fake"]
QualityAnalyzerKind = Literal["real", "fake"]

_SEPARATORS: tuple[SeparatorKind, ...] = ("demucs", "fake")
_DEVICES: tuple[DeviceKind, ...] = ("cpu", "cuda", "auto")
_FAKE_DEVICES: tuple[FakeDeviceKind, ...] = ("cpu", "gpu", "gpu-oom")
_FAKE_ONLINE: tuple[FakeOnlineMode, ...] = ("found", "not-found", "failed", "off")
_ON_OFF: tuple[str, ...] = ("on", "off")
_ALIGN_MODELS: tuple[AlignModel, ...] = ("turbo", "small")
_ALIGN_AUTO: tuple[AlignAutoMode, ...] = ("gpu", "always", "never")
_FAKE_LYRICS: tuple[FakeLyricsMode, ...] = (
    "word", "line", "plain", "instrumental", "not-found", "failed",
)  # fmt: skip
_TRANSCRIBERS: tuple[str, ...] = ("", "real", "fake")
_STRUCTURE_ANALYZERS: tuple[str, ...] = ("", "real", "fake")
_CHORD_ENGINES: tuple[str, ...] = ("", "btc", "chroma-hmm", "fake")
_PITCH_ENGINES: tuple[str, ...] = ("", "pyin", "yin", "fake")
_QUALITY_ANALYZERS: tuple[str, ...] = ("", "real", "fake")
_DEFAULT_ANALYSIS_THREADS = 4
_DEFAULT_INSIGHTS_THREADS = 4
_MAX_INSIGHTS_THREADS = 16
_MAX_THREADS = 64
_LANGUAGE = re.compile(r"[a-z]{2,3}")
_MAX_LANGUAGES = 5


@dataclass(frozen=True, slots=True)
class OnlineSettings:
    """Online lookups of feature 005 (data-model.md 1).

    Attributes:
        lookups_enabled: False when ``STEMSTUDIO_ONLINE_LOOKUPS=off``: no request to MusicBrainz,
            Cover Art Archive, Wikidata or Wikipedia; metadata jobs complete as ``disabled``.
        contact_url: Raw ``STEMSTUDIO_CONTACT_URL`` (unset -> None). Validated by the
            User-Agent builder (an invalid value is replaced by the generic token and logged
            without the value); never an e-mail address.
        wikipedia_languages: Wiki language codes in the order they are tried
            (``STEMSTUDIO_WIKIPEDIA_LANGUAGES``, comma list, 1..5 codes of 2-3 letters).
        fake_mode: Behaviour of the fake metadata provider and fake source artwork
            (``STEMSTUDIO_FAKE_ONLINE``); only used with ``separator=fake``.
    """

    lookups_enabled: bool = True
    contact_url: str | None = None
    wikipedia_languages: tuple[str, ...] = ("en",)
    fake_mode: FakeOnlineMode = "found"


@dataclass(frozen=True, slots=True)
class RenderSettings:
    """Render jobs of feature 007 (data-model.md 1).

    Attributes:
        library_dir: Read-only root of the API data directory (``STEMSTUDIO_LIBRARY_DIR``);
            None when unset, in which case render jobs fail with the "library is not available"
            text and ``/health`` reports ``library.readable = false``.
        block_seconds: Block length of the streaming renderer
            (``STEMSTUDIO_RENDER_BLOCK_SECONDS``, > 0; tests use small values).
    """

    library_dir: Path | None = None
    block_seconds: float = 10.0


@dataclass(frozen=True, slots=True)
class LyricsSettings:
    """Lyrics alignment of feature 006 (data-model.md 1).

    Attributes:
        align_model: Whisper model of the alignment (``STEMSTUDIO_ALIGN_MODEL``): ``turbo``
            (large-v3-turbo, default) or ``small``.
        align_auto: When an alignment starts without a click (``STEMSTUDIO_ALIGN_AUTO``):
            ``gpu`` (only on a GPU worker, default), ``always`` or ``never``; reported in
            ``/health`` for the backend's auto rule.
        fake_mode: Behaviour of the fake LRCLIB provider and the fake aligner
            (``STEMSTUDIO_FAKE_LYRICS``); only used with ``separator=fake``.
    """

    align_model: AlignModel = "turbo"
    align_auto: AlignAutoMode = "gpu"
    fake_mode: FakeLyricsMode = "word"


@dataclass(frozen=True, slots=True)
class TranscriptionSettings:
    """Sheet music transcription of feature 008 (data-model.md 1).

    Attributes:
        transcriber: ``real`` models or the deterministic ``fake`` transcriber
            (``STEMSTUDIO_TRANSCRIBER``; unset = ``fake`` with ``separator=fake``, else ``real``).
            Notation and engraving are always real.
        threads: torch/onnxruntime intra-op threads of the transcribe lane
            (``STEMSTUDIO_TRANSCRIBE_THREADS``, 1..64, default 8).
    """

    transcriber: TranscriberKind = "real"
    threads: int = 8


@dataclass(frozen=True, slots=True)
class InstrumentAnalysisSettings:
    """Settings of the ``instrument-analysis`` lane (feature 016, data-model 1).

    Attributes:
        chord_engine: ``STEMSTUDIO_CHORD_ENGINE``: ``btc`` (falls back to ``chroma-hmm`` per
            job when the weights are unavailable), ``chroma-hmm`` or ``fake``; unset = ``fake`` with
            ``separator=fake``, else ``btc``.
        pitch_engine: ``STEMSTUDIO_PITCH_ENGINE``: ``pyin``, ``yin`` (fast) or ``fake``; unset =
            ``fake`` with ``separator=fake``, else ``pyin``.
        threads: torch/numpy threads of the lane (``STEMSTUDIO_ANALYSIS_THREADS``, 1..64; unset or
            blank = 4).
    """

    chord_engine: ChordEngineKind = "btc"
    pitch_engine: PitchEngineKind = "pyin"
    threads: int = _DEFAULT_ANALYSIS_THREADS


@dataclass(frozen=True, slots=True)
class InsightsSettings:
    """Settings of the ``insights`` lane (feature 020, data-model 1).

    Attributes:
        quality_analyzer: ``STEMSTUDIO_QUALITY_ANALYZER``: ``real`` signal measures or the
            deterministic ``fake`` (data-model 2.6); unset = ``fake`` with ``separator=fake``,
            else ``real``.
        threads: numpy/scipy BLAS threads of the lane (``STEMSTUDIO_INSIGHTS_THREADS``, 1..16;
            unset or blank = 4).
    """

    quality_analyzer: QualityAnalyzerKind = "real"
    threads: int = _DEFAULT_INSIGHTS_THREADS


@dataclass(frozen=True, slots=True)
class WorkerSettings:
    """Runtime settings of the worker.

    Attributes:
        data_dir: Root of jobs, cache and temporary files (``STEMSTUDIO_DATA_DIR``).
        separator: ``demucs`` (real model) or ``fake`` (copies the mix into stems; for CI/E2E)
            (``STEMSTUDIO_SEPARATOR``).
        device: Torch device preference (``STEMSTUDIO_DEVICE``). ``cuda`` and ``auto`` both use
            the GPU when the start-up probe finds a usable one and otherwise run on the CPU with
            a reason in ``/health`` (feature 003, FR-003); ``cpu`` never touches CUDA.
        max_duration_seconds: Longest accepted audio (15 minutes).
        max_upload_bytes: Largest accepted upload (200 MiB).
        primary_model: Demucs model tried first.
        fallback_model: Demucs model used when the primary cannot be loaded.
        model_dir: Root of downloaded detail model weights (``STEMSTUDIO_MODEL_DIR``); never
            inside the repository (constitution IV).
        fake_detail_step_seconds: Delay per parent step of the fake detail models
            (``STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS``), so E2E tests can observe progress.
        fake_device: Device the fake separator pretends to use (``STEMSTUDIO_FAKE_DEVICE``):
            ``cpu`` (default), ``gpu`` (a fake GPU) or ``gpu-oom`` (a fake GPU whose first model
            step of every job runs out of memory and is retried on the CPU); only with
            ``separator=fake``, for E2E tests of the device display (feature 003).
        online: Online lookup settings (feature 005).
        render: Render job settings (feature 007).
        lyrics: Lyrics alignment settings (feature 006).
        transcription: Transcription settings (feature 008).
        gpu_lease_file: Shared cross-container GPU lock file (``STEMSTUDIO_GPU_LEASE_FILE``,
            feature 009, ADR-018 D3); None (unset) keeps the 003 in-process lease only. Set in
            the GPU image together with the optional generator service.
        gpu_lease_wait_seconds: Longest wait for the file lease before a GPU step runs on the
            CPU with the reason "GPU busy with another StemStudio job"
            (``STEMSTUDIO_GPU_LEASE_WAIT_SECONDS``, default 900).
        structure_analyzer: Section detection of feature 014 (``STEMSTUDIO_STRUCTURE_ANALYZER``):
            ``real`` (``stems-novelty``) or the deterministic ``fake`` (data-model 2.4); unset =
            ``fake`` with ``separator=fake``, else ``real``.
        instrument_analysis: Chord, pitch and thread settings of feature 016.
        insights: Quality analyzer and thread settings of feature 020.
    """

    data_dir: Path
    separator: SeparatorKind = "demucs"
    device: DeviceKind = "cpu"
    max_duration_seconds: float = 900.0
    max_upload_bytes: int = 200 * 1024 * 1024
    primary_model: str = "htdemucs_6s"
    fallback_model: str = "htdemucs"
    model_dir: Path = Path("data/models")
    fake_detail_step_seconds: float = 1.0
    fake_device: FakeDeviceKind = "cpu"
    online: OnlineSettings = OnlineSettings()
    render: RenderSettings = RenderSettings()
    lyrics: LyricsSettings = LyricsSettings()
    transcription: TranscriptionSettings = TranscriptionSettings()
    gpu_lease_file: Path | None = None
    gpu_lease_wait_seconds: float = 900.0
    structure_analyzer: StructureAnalyzerKind = "real"
    instrument_analysis: InstrumentAnalysisSettings = InstrumentAnalysisSettings()
    insights: InsightsSettings = InsightsSettings()


GPU_LEASE_BUSY_REASON = "GPU busy with another StemStudio job"
"""CPU fallback reason after waiting ``gpu_lease_wait_seconds`` for the file lease (009 DELTA 3)."""


def load_settings(environ: Mapping[str, str] | None = None) -> WorkerSettings:
    """Build settings from environment variables.

    Args:
        environ: Variables to read; defaults to ``os.environ``.

    Returns:
        The validated settings.

    Raises:
        ValueError: A variable has a value outside its allowed set.
    """
    env = os.environ if environ is None else environ
    separator = cast(SeparatorKind, _choice(env, "STEMSTUDIO_SEPARATOR", _SEPARATORS))
    return WorkerSettings(
        data_dir=Path(env.get("STEMSTUDIO_DATA_DIR", "data/worker")),
        separator=separator,
        device=cast(DeviceKind, _choice(env, "STEMSTUDIO_DEVICE", _DEVICES)),
        model_dir=Path(env.get("STEMSTUDIO_MODEL_DIR", "data/models")),
        **_fake_settings(env),
        online=_online_settings(env),
        render=_render_settings(env),
        lyrics=_lyrics_settings(env),
        **_analysis_settings(env, separator),
        **_gpu_lease_settings(env),
    )


class _FakeSettings(TypedDict):
    """The fake separator variables of ``WorkerSettings``."""

    fake_detail_step_seconds: float
    fake_device: FakeDeviceKind


class _GpuLeaseSettings(TypedDict):
    """The cross-container GPU lease variables of ``WorkerSettings`` (feature 009)."""

    gpu_lease_file: Path | None
    gpu_lease_wait_seconds: float


def _fake_settings(env: Mapping[str, str]) -> _FakeSettings:
    """Read ``STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS`` and ``STEMSTUDIO_FAKE_DEVICE``."""
    return {
        "fake_detail_step_seconds": _seconds(env, "STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS", 1.0),
        "fake_device": cast(FakeDeviceKind, _choice(env, "STEMSTUDIO_FAKE_DEVICE", _FAKE_DEVICES)),
    }


def _gpu_lease_settings(env: Mapping[str, str]) -> _GpuLeaseSettings:
    """Read ``STEMSTUDIO_GPU_LEASE_FILE`` (blank -> None) and ``..._GPU_LEASE_WAIT_SECONDS``."""
    return {
        "gpu_lease_file": _optional_path(env, "STEMSTUDIO_GPU_LEASE_FILE"),
        "gpu_lease_wait_seconds": _seconds(env, "STEMSTUDIO_GPU_LEASE_WAIT_SECONDS", 900.0),
    }


def _optional_path(env: Mapping[str, str], name: str) -> Path | None:
    """Read ``name`` as a path; unset or blank -> None."""
    raw = env.get(name, "").strip()
    return Path(raw) if raw else None


def _online_settings(env: Mapping[str, str]) -> OnlineSettings:
    """Read the feature 005 variables (``STEMSTUDIO_ONLINE_LOOKUPS`` ... ``FAKE_ONLINE``)."""
    contact = env.get("STEMSTUDIO_CONTACT_URL", "").strip()
    return OnlineSettings(
        lookups_enabled=_choice(env, "STEMSTUDIO_ONLINE_LOOKUPS", _ON_OFF) == "on",
        contact_url=contact or None,
        wikipedia_languages=_languages(env.get("STEMSTUDIO_WIKIPEDIA_LANGUAGES", "en")),
        fake_mode=cast(FakeOnlineMode, _choice(env, "STEMSTUDIO_FAKE_ONLINE", _FAKE_ONLINE)),
    )


def _render_settings(env: Mapping[str, str]) -> RenderSettings:
    """Read the feature 007 variables (``STEMSTUDIO_LIBRARY_DIR``, ``..._RENDER_BLOCK_SECONDS``)."""
    library = env.get("STEMSTUDIO_LIBRARY_DIR", "").strip()
    block = _seconds(env, "STEMSTUDIO_RENDER_BLOCK_SECONDS", 10.0)
    if block == 0:
        msg = "STEMSTUDIO_RENDER_BLOCK_SECONDS must be greater than 0"
        raise ValueError(msg)
    return RenderSettings(library_dir=Path(library) if library else None, block_seconds=block)


def _lyrics_settings(env: Mapping[str, str]) -> LyricsSettings:
    """Read the feature 006 variables (``STEMSTUDIO_ALIGN_MODEL`` ... ``FAKE_LYRICS``)."""
    return LyricsSettings(
        align_model=cast(AlignModel, _choice(env, "STEMSTUDIO_ALIGN_MODEL", _ALIGN_MODELS)),
        align_auto=cast(AlignAutoMode, _choice(env, "STEMSTUDIO_ALIGN_AUTO", _ALIGN_AUTO)),
        fake_mode=cast(FakeLyricsMode, _choice(env, "STEMSTUDIO_FAKE_LYRICS", _FAKE_LYRICS)),
    )


def _transcription_settings(
    env: Mapping[str, str], separator: SeparatorKind
) -> TranscriptionSettings:
    """Read the feature 008 variables (``STEMSTUDIO_TRANSCRIBER``, ``..._TRANSCRIBE_THREADS``)."""
    kind = _choice(env, "STEMSTUDIO_TRANSCRIBER", _TRANSCRIBERS) or (
        "fake" if separator == "fake" else "real"
    )
    raw = env.get("STEMSTUDIO_TRANSCRIBE_THREADS", "8").strip()
    if not raw.isdigit() or not 0 < int(raw) <= _MAX_THREADS:
        msg = f"STEMSTUDIO_TRANSCRIBE_THREADS must be a whole number from 1 to 64; got {raw!r}"
        raise ValueError(msg)
    return TranscriptionSettings(transcriber=cast(TranscriberKind, kind), threads=int(raw))


class _AnalysisSettings(TypedDict):
    """The model choices of ``WorkerSettings`` that follow the separator when unset."""

    transcription: TranscriptionSettings
    structure_analyzer: StructureAnalyzerKind
    instrument_analysis: InstrumentAnalysisSettings
    insights: InsightsSettings


def _analysis_settings(env: Mapping[str, str], separator: SeparatorKind) -> _AnalysisSettings:
    """Read the 008 transcription, 014 structure, 016 analysis and 020 insights variables."""
    return {
        "transcription": _transcription_settings(env, separator),
        "structure_analyzer": _structure_analyzer(env, separator),
        "instrument_analysis": _instrument_analysis_settings(env, separator),
        "insights": _insights_settings(env, separator),
    }


def _insights_settings(env: Mapping[str, str], separator: SeparatorKind) -> InsightsSettings:
    """Read ``STEMSTUDIO_QUALITY_ANALYZER`` and ``STEMSTUDIO_INSIGHTS_THREADS`` (feature 020)."""
    kind = _choice(env, "STEMSTUDIO_QUALITY_ANALYZER", _QUALITY_ANALYZERS)
    raw = env.get("STEMSTUDIO_INSIGHTS_THREADS", "").strip() or str(_DEFAULT_INSIGHTS_THREADS)
    if not raw.isdigit() or not 0 < int(raw) <= _MAX_INSIGHTS_THREADS:
        msg = f"STEMSTUDIO_INSIGHTS_THREADS must be a whole number from 1 to 16; got {raw!r}"
        raise ValueError(msg)
    return InsightsSettings(
        quality_analyzer=cast(
            QualityAnalyzerKind, kind or ("fake" if separator == "fake" else "real")
        ),
        threads=int(raw),
    )


def _instrument_analysis_settings(
    env: Mapping[str, str], separator: SeparatorKind
) -> InstrumentAnalysisSettings:
    """Read ``STEMSTUDIO_CHORD_ENGINE``, ``..._PITCH_ENGINE`` and ``..._ANALYSIS_THREADS`` (016)."""
    fake = separator == "fake"
    chord = _choice(env, "STEMSTUDIO_CHORD_ENGINE", _CHORD_ENGINES) or ("fake" if fake else "btc")
    pitch = _choice(env, "STEMSTUDIO_PITCH_ENGINE", _PITCH_ENGINES) or ("fake" if fake else "pyin")
    return InstrumentAnalysisSettings(
        chord_engine=cast(ChordEngineKind, chord),
        pitch_engine=cast(PitchEngineKind, pitch),
        threads=_analysis_threads(env),
    )


def _analysis_threads(env: Mapping[str, str]) -> int:
    """Read ``STEMSTUDIO_ANALYSIS_THREADS`` (1..64); unset or blank = 4."""
    raw = env.get("STEMSTUDIO_ANALYSIS_THREADS", "").strip() or str(_DEFAULT_ANALYSIS_THREADS)
    if not raw.isdigit() or not 0 < int(raw) <= _MAX_THREADS:
        msg = f"STEMSTUDIO_ANALYSIS_THREADS must be a whole number from 1 to 64; got {raw!r}"
        raise ValueError(msg)
    return int(raw)


def _structure_analyzer(env: Mapping[str, str], separator: SeparatorKind) -> StructureAnalyzerKind:
    """Read ``STEMSTUDIO_STRUCTURE_ANALYZER`` (feature 014); unset follows the separator."""
    kind = _choice(env, "STEMSTUDIO_STRUCTURE_ANALYZER", _STRUCTURE_ANALYZERS)
    return cast(StructureAnalyzerKind, kind or ("fake" if separator == "fake" else "real"))


def _languages(raw: str) -> tuple[str, ...]:
    """Parse a comma list of 1..5 wiki language codes (2-3 lower-case letters)."""
    codes = tuple(code.strip().lower() for code in raw.split(",") if code.strip())
    if not 0 < len(codes) <= _MAX_LANGUAGES or not all(map(_LANGUAGE.fullmatch, codes)):
        msg = f"STEMSTUDIO_WIKIPEDIA_LANGUAGES must list 1-5 language codes; got {raw!r}"
        raise ValueError(msg)
    return codes


def _choice(env: Mapping[str, str], name: str, allowed: tuple[str, ...]) -> str:
    """Read ``name`` (case-insensitive value) and check it against ``allowed``; first is default."""
    value = env.get(name, allowed[0]).strip().lower()
    if value not in allowed:
        msg = f"{name} must be one of {', '.join(allowed)}; got {value!r}"
        raise ValueError(msg)
    return value


def _seconds(env: Mapping[str, str], name: str, default: float) -> float:
    """Read ``name`` as a non-negative number of seconds; ``default`` when unset."""
    raw = env.get(name)
    if raw is None:
        return default
    try:
        value = float(raw)
    except ValueError:
        value = -1.0
    if value < 0:
        msg = f"{name} must be a non-negative number of seconds; got {raw!r}"
        raise ValueError(msg)
    return value
