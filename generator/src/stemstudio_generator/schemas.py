"""Pydantic v2 models of the generator HTTP contract.

These models mirror ``specs/010-lyrics-to-song/contracts/generator-api.openapi.yaml`` (1.1.0),
which contains feature 009's 1.0.0 document (``specs/009-style-transfer/contracts``) plus the
song and voice-conversion kinds and the voice-sample checks of feature 010. They are the single
source of truth for JSON on the wire (camelCase aliases) and for the job files on disk. Models
are immutable; use ``model.model_copy(update={...})`` to derive new states.
"""

from datetime import datetime
from typing import Annotated, Final, Literal, Self

from pydantic import (
    BaseModel,
    ConfigDict,
    Field,
    SerializerFunctionWrapHandler,
    TypeAdapter,
    model_serializer,
    model_validator,
)
from pydantic.alias_generators import to_camel

CONTRACT_VERSION: Final = "1.1.0"
"""Version of the generator API contract (``info.version`` in the OpenAPI document)."""

TAKE_SAMPLE_RATE: Final = 48_000
"""Sample rate of every take (Hz); FLAC 24-bit stereo."""

MIN_RANGE_SECONDS: Final = 10.0
"""Shortest restyle range and song (seconds)."""

MAX_RANGE_SECONDS: Final = 480.0
"""Longest restyle range and song (seconds)."""

GenerationKind = Literal["restyle", "song", "voiceConversion"]
"""Job kinds (``restyle`` 1.0.0; ``song`` and ``voiceConversion`` 1.1.0)."""

GenerationStatus = Literal[
    "queued", "waitingForGpu", "downloadingModel", "running", "completed", "failed", "cancelled"
]
"""Job states (009 data-model.md 2.4); the last three are terminal."""

TERMINAL_STATUSES: Final[frozenset[str]] = frozenset({"completed", "failed", "cancelled"})
"""States a job never leaves."""

GenerationErrorCode = Literal[
    "gpuRequired", "gpuBusy", "outOfMemory", "modelDownloadFailed", "interrupted",
    "invalidInput", "engineError", "unvoicedTarget", "promptInvalid",
]  # fmt: skip
"""Codes of ``Generation.error`` (``unvoicedTarget``, ``promptInvalid`` 1.1.0)."""

GeneratorProblemCode = Literal["invalidInput", "gpuRequired", "sourceTooShort", "kindNotServed"]
"""Extension member ``code`` of the problem details of refused requests."""

GeneratorEngine = Literal["acestep", "soulx", "fake"]
"""Engine behind a service instance (``STEMSTUDIO_GENERATOR``)."""

ModelId = Literal[
    "acestep-v15-turbo", "acestep-vae", "qwen3-embedding-0.6b", "acestep-5hz-lm-1.7b",
    "soulx-singer-svc", "rmvpe", "whisper-base",
]  # fmt: skip
"""Model files a service may need (ACE-Step bundle; the singer's models 1.1.0)."""

ProcessingDeviceKind = Literal["cpu", "gpu"]
"""The CPU or the NVIDIA GPU (CUDA)."""

RestyleMode = Literal["range", "inPlace"]
"""``range``: the range becomes the take (``cover``); ``inPlace``: only the range is repainted."""

VocalLabel = Literal["none", "synthetic", "converted"]
"""AI voice label of a take (1.1.0)."""

SongProfile = Literal["planned", "direct"]
"""Song profile that ran: DiT + language-model planner, or the DiT alone (1.1.0)."""

VoiceSampleIssue = Literal["tooShort", "tooLong", "unvoiced"]
"""Why a voice sample is not usable (1.1.0)."""

TimeSignature = Literal["2", "3", "4", "6"]
"""Beats per bar of a song (2/4, 3/4, 4/4, 6/8)."""

VocalLanguage = Literal[
    "ar", "az", "bg", "bn", "ca", "cs", "da", "de", "el", "en", "es", "fa", "fi", "fr", "he", "hi",
    "hr", "ht", "hu", "id", "is", "it", "ja", "ko", "la", "lt", "ms", "ne", "nl", "no", "pa", "pl",
    "pt", "ro", "ru", "sa", "sk", "sr", "sv", "sw", "ta", "te", "th", "tl", "tr", "uk", "ur", "vi",
    "yue", "zh", "unknown",
]  # fmt: skip
"""Lyrics languages of ACE-Step 1.5 (``acestep/constants.py`` ``VALID_LANGUAGES``)."""

Hex32 = Annotated[str, Field(pattern=r"^[0-9a-f]{32}$")]
"""Job and check identifiers: 32 lower-case hex characters (a UUID without dashes)."""

Sha256 = Annotated[str, Field(pattern=r"^[0-9a-f]{64}$")]
"""SHA-256 digest: 64 lower-case hex characters."""

Seed = Annotated[int, Field(ge=0, le=2_147_483_647)]
"""Generation seed."""

Seeds = Annotated[list[Seed], Field(min_length=1, max_length=2)]
"""One seed per take (1-2 takes)."""

Caption = Annotated[str, Field(min_length=1, max_length=512)]
"""Model caption built by the backend."""

TakeIndex = Annotated[int, Field(ge=1, le=2)]
"""Take number, starting at 1."""

# ------------------------------------------------------------------ fixed texts

GPU_REQUIRED_TITLE: Final = "GPU required"
"""Problem title of ``409 gpuRequired``; the detail is the probe reason."""

KIND_NOT_SERVED_TITLE: Final = "Kind not served"
"""Problem title of ``409 kindNotServed`` (1.1.0)."""

OUT_OF_MEMORY_TEXT: Final = (
    "Not enough GPU memory for this request (try a shorter range or 1 take)."
)
"""Error message after a second GPU out-of-memory (ADR-018 D4)."""

GPU_BUSY_TEMPLATE: Final = "GPU busy: needs {need:.1f} GB, {free:.1f} GB free."
"""Error message after the free-memory wait limit (``str.format(need=, free=)`` in GB)."""

INTERRUPTED_TEXT: Final = "The generator restarted while this job was running; try again."
"""Error message of jobs found active at start-up (restart reconciliation)."""

UNVOICED_TARGET_TEXT: Final = (
    "The vocal stem of this take has almost no singing (less than 10 % voiced); "
    "there is nothing to convert."
)
"""Error message of ``unvoicedTarget`` (1.1.0)."""

LOW_MEMORY_RETRY_STAGE: Final = "Retrying with low memory"
"""Stage text of the one low-memory retry after a GPU out-of-memory."""

PLANNING_STAGE: Final = "Planning song structure"
"""Stage text while the language-model planner runs (1.1.0)."""

CLIPPED_WARNING: Final = "The summed stems clipped; peaks were limited."
"""Warning when the summed source stems exceeded full scale."""


class ContractModel(BaseModel):
    """Base model: camelCase JSON aliases, unknown fields rejected, immutable."""

    model_config = ConfigDict(
        alias_generator=to_camel,
        populate_by_name=True,
        extra="forbid",
        frozen=True,
    )


# ------------------------------------------------------------------ health


class DeviceInfo(ContractModel):
    """Device found by the start-up probe (same shape and texts as the worker's ``DeviceInfo``)."""

    kind: ProcessingDeviceKind
    name: Annotated[str | None, Field(max_length=200)] = None
    compute_capability: Annotated[str | None, Field(pattern=r"^[0-9]{1,2}\.[0-9]{1,2}$")] = None
    total_memory_mib: Annotated[int | None, Field(ge=0)] = Field(
        default=None, alias="totalMemoryMiB"
    )
    free_memory_mib: Annotated[int | None, Field(ge=0)] = Field(default=None, alias="freeMemoryMiB")
    reason: Annotated[str | None, Field(max_length=500)] = None


class ModelStatus(ContractModel):
    """One model file set: whether it is in the cache, its download size and pinned revision."""

    id: ModelId
    present: bool
    bytes: Annotated[int, Field(ge=0)]
    revision: Annotated[str, Field(max_length=64)]


class QueueStatus(ContractModel):
    """Queue length (excluding the running job) and the id of the job being processed."""

    queued: Annotated[int, Field(ge=0)]
    running: Hex32 | None = None


class GeneratorHealth(ContractModel):
    """Body of ``GET /health``; ``ready`` iff ``device.kind`` is ``gpu``."""

    status: Literal["ok"] = "ok"
    contract_version: str = CONTRACT_VERSION
    engine: GeneratorEngine
    kinds: Annotated[list[GenerationKind], Field(min_length=1, max_length=3)]
    voice_sample_check: bool
    ready: bool
    reason: Annotated[str | None, Field(max_length=500)] = None
    device: DeviceInfo
    models: Annotated[list[ModelStatus], Field(max_length=16)]
    queue: QueueStatus

    @model_validator(mode="after")
    def _ready_needs_gpu(self) -> Self:
        if self.ready != (self.device.kind == "gpu"):
            msg = "ready must be true exactly when device.kind is gpu"
            raise ValueError(msg)
        return self


# ------------------------------------------------------------------ requests


class RestyleParams(ContractModel):
    """Parameters of a ``restyle`` job (ACE-Step ``cover`` or ``repaint``)."""

    caption: Caption
    audio_cover_strength: Annotated[float, Field(ge=0, le=1)]
    inference_steps: Annotated[int, Field(ge=1, le=20)] = 8
    shift: Annotated[float, Field(ge=1, le=5)] = 3.0
    seeds: Seeds
    mode: RestyleMode
    range_start_seconds: Annotated[float, Field(ge=0)]
    range_end_seconds: Annotated[float, Field(gt=0)]

    @model_validator(mode="after")
    def _range_length(self) -> Self:
        length = self.range_end_seconds - self.range_start_seconds
        if not MIN_RANGE_SECONDS <= length <= MAX_RANGE_SECONDS:
            msg = "The range must be between 10 and 480 seconds long."
            raise ValueError(msg)
        return self


class SongParams(ContractModel):
    """Parameters of a ``song`` job (ACE-Step ``text2music``, 1.1.0)."""

    caption: Caption
    lyrics: Annotated[str, Field(min_length=1, max_length=4096)]
    instrumental: bool
    vocal_language: VocalLanguage
    bpm: Annotated[int | None, Field(ge=40, le=220)] = None
    key_scale: Annotated[str | None, Field(pattern=r"^[A-G](#|b)? (major|minor)$")] = None
    time_signature: TimeSignature | None = None
    duration_seconds: Annotated[float, Field(ge=MIN_RANGE_SECONDS, le=MAX_RANGE_SECONDS)]
    inference_steps: Annotated[int, Field(ge=1, le=20)] = 8
    shift: Annotated[float, Field(ge=1, le=5)] = 3.0
    seeds: Seeds
    thinking: bool = True


class VoiceConversionParams(ContractModel):
    """Parameters of a ``voiceConversion`` job (SoulX-Singer-SVC, singer service, 1.1.0)."""

    pitch_shift: Annotated[int, Field(ge=-12, le=12)] = 0
    auto_shift: bool = True
    steps: Annotated[int, Field(ge=8, le=64)] = 32
    cfg: Annotated[float, Field(ge=1, le=6)] = 3.0
    vocal_gain_db: Annotated[float, Field(ge=-12, le=6)] = 0
    profile_name: Annotated[str, Field(min_length=1, max_length=80)]


class RestyleGenerationRequest(ContractModel):
    """``params`` part of a restyle request."""

    kind: Literal["restyle"] = "restyle"
    restyle: RestyleParams


class SongGenerationRequest(ContractModel):
    """JSON body of a song request (1.1.0)."""

    kind: Literal["song"] = "song"
    song: SongParams


class VoiceConversionGenerationRequest(ContractModel):
    """``params`` part of a voice-conversion request (1.1.0)."""

    kind: Literal["voiceConversion"] = "voiceConversion"
    voice_conversion: VoiceConversionParams


CreateGenerationRequest = Annotated[
    RestyleGenerationRequest | SongGenerationRequest | VoiceConversionGenerationRequest,
    Field(discriminator="kind"),
]
"""The request of ``POST /v1/generations``, discriminated by ``kind``."""

CREATE_GENERATION_REQUEST: Final[TypeAdapter[CreateGenerationRequest]] = TypeAdapter(
    CreateGenerationRequest
)
"""Parses and validates a ``CreateGenerationRequest`` (JSON body or ``params`` part)."""


# ------------------------------------------------------------------ jobs


class JobDevice(ContractModel):
    """Device record of a generation; ``cpu_fallback`` is always false (no CPU path)."""

    kind: ProcessingDeviceKind
    name: Annotated[str | None, Field(max_length=200)] = None
    cpu_fallback: bool = False
    fallback_reason: Annotated[str | None, Field(max_length=500)] = None


class GenerationError(ContractModel):
    """Why a job failed; the message is shown to the user verbatim."""

    code: GenerationErrorCode
    message: Annotated[str, Field(max_length=500)]


class TakeModel(ContractModel):
    """Model that produced a take."""

    id: Annotated[str, Field(max_length=64)]
    revision: Annotated[str, Field(max_length=64)]


class GeneratedTake(ContractModel):
    """One take of a completed job (audio at ``GET /v1/generations/{id}/takes/{index}``)."""

    index: TakeIndex
    seed: Seed
    duration_seconds: Annotated[float, Field(gt=0)]
    sample_rate: Literal[48000] = TAKE_SAMPLE_RATE
    sha256: Sha256
    bytes: Annotated[int, Field(ge=1)]
    peak_dbfs: Annotated[float, Field(le=0)]
    model: TakeModel
    vocal: VocalLabel | None = None
    has_vocal_stem: bool = False
    profile: SongProfile | None = None


_KIND_MEMBERS: Final[dict[str, str]] = {
    "restyle": "restyle",
    "song": "song",
    "voiceConversion": "voice_conversion",
}


class Generation(ContractModel):
    """A generation job: body of create, get and cancel."""

    generation_id: Hex32
    kind: GenerationKind
    status: GenerationStatus
    progress: Annotated[int, Field(ge=0, le=100)]
    stage: Annotated[str, Field(max_length=200)]
    restyle: RestyleParams | None = None
    song: SongParams | None = None
    voice_conversion: VoiceConversionParams | None = None
    takes: Annotated[list[GeneratedTake], Field(max_length=2)] = []
    warnings: Annotated[list[Annotated[str, Field(max_length=300)]], Field(max_length=5)] = []
    device: JobDevice | None = None
    error: GenerationError | None = None
    created_at: datetime
    updated_at: datetime

    @model_serializer(mode="wrap")
    def _drop_other_kinds(self, handler: SerializerFunctionWrapHandler) -> dict[str, object]:
        """Serialise only the parameter member of ``kind`` (the others are absent, not null)."""
        data: dict[str, object] = handler(self)
        for member in ("restyle", "song", "voice_conversion", "voiceConversion"):
            if data.get(member, ...) is None:
                del data[member]
        return data

    @model_validator(mode="after")
    def _one_member_per_kind(self) -> Self:
        present = {name for name in _KIND_MEMBERS.values() if getattr(self, name) is not None}
        if present != {_KIND_MEMBERS[self.kind]}:
            msg = f"exactly the member of kind {self.kind} must be set"
            raise ValueError(msg)
        return self

    @model_validator(mode="after")
    def _error_iff_failed(self) -> Self:
        if (self.error is not None) != (self.status == "failed"):
            msg = "error must be set exactly when status is failed"
            raise ValueError(msg)
        return self


# ------------------------------------------------------------------ voice samples (1.1.0)


class VoiceSampleCheck(ContractModel):
    """Result of ``POST /v1/voice-samples/check``; ``reason``/``message`` iff not ``ok``."""

    check_id: Hex32
    duration_seconds: Annotated[float, Field(ge=0)]
    voiced_ratio: Annotated[float, Field(ge=0, le=1)]
    pcm_sha256: Sha256
    ok: bool
    reason: VoiceSampleIssue | None = None
    message: Annotated[str | None, Field(max_length=300)] = None

    @model_validator(mode="after")
    def _reason_iff_not_ok(self) -> Self:
        if self.ok == (self.reason is not None) or (self.reason is None) != (self.message is None):
            msg = "reason and message must be set exactly when ok is false"
            raise ValueError(msg)
        return self


class Problem(ContractModel):
    """RFC 9457 problem details with the extension member ``code`` (error responses)."""

    model_config = ConfigDict(extra="allow", frozen=True)

    type: str = "about:blank"
    title: str
    status: Annotated[int, Field(ge=400, le=599)]
    detail: str | None = None
    code: GeneratorProblemCode | None = None

    @model_serializer(mode="wrap")
    def _drop_missing_code(self, handler: SerializerFunctionWrapHandler) -> dict[str, object]:
        """Leave ``code`` out when it is not set."""
        data: dict[str, object] = handler(self)
        if data.get("code", ...) is None:
            del data["code"]
        return data
