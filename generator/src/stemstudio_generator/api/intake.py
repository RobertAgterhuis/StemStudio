"""Per-kind intake of ``createGeneration``: save and check the uploaded files of a new job.

Each intake runs after the request was validated, the kind is served and the GPU is ready. It
saves the parts into the job's ``inputs/`` directory and refuses unusable audio before the job
is queued (``400``/``413``/``415``/``422``).
"""

from collections.abc import Awaitable, Callable
from pathlib import Path
from typing import Final

from starlette.concurrency import run_in_threadpool
from starlette.datastructures import UploadFile

from stemstudio_generator.api.context import ApiContext
from stemstudio_generator.api.problems import invalid_input, source_too_short
from stemstudio_generator.api.uploads import GenerationUpload, audio_parts, save_audio_parts
from stemstudio_generator.ports import InvalidInputError
from stemstudio_generator.schemas import RestyleGenerationRequest, VoiceConversionGenerationRequest

Intake = Callable[[ApiContext, GenerationUpload, str], Awaitable[None]]
"""``(context, upload, generation_id)``: save and check the job's inputs."""

_RANGE_TOLERANCE_SECONDS: Final = 1e-3
MAX_ACCOMPANIMENT: Final = 5
ACCOMPANIMENT_TEXT: Final = "A voice conversion takes 0 to 5 accompaniment stems."
TARGET_COUNT_TEXT: Final = "A voice conversion needs exactly one target part (the vocal stem)."
PROMPT_COUNT_TEXT: Final = "A voice conversion needs exactly one prompt part (the voice sample)."
TARGET_TOO_LONG_TEXT: Final = "The vocal stem is longer than {limit:g} s ({seconds:.1f} s)."


async def restyle_intake(context: ApiContext, upload: GenerationUpload, generation_id: str) -> None:
    """Save the 1-6 ``stem`` parts and check that they sum and contain the range."""
    request = upload.request
    if not isinstance(request, RestyleGenerationRequest):  # pragma: no cover - routed by kind
        raise TypeError(request)
    stems = await save_audio_parts(
        audio_parts(upload.form, "stem"), context.files.inputs(generation_id), "stem",
        context.limits.stem_bytes,
    )  # fmt: skip
    try:
        seconds = await run_in_threadpool(context.mixer.measure, stems)
    except InvalidInputError as error:
        raise invalid_input(str(error)) from error
    end = request.restyle.range_end_seconds
    if end > seconds + _RANGE_TOLERANCE_SECONDS:
        raise source_too_short(end, seconds)


async def song_intake(context: ApiContext, upload: GenerationUpload, generation_id: str) -> None:
    """A song has no files (JSON body); nothing to save."""
    del context, upload, generation_id


async def conversion_intake(
    context: ApiContext, upload: GenerationUpload, generation_id: str
) -> None:
    """Save ``target``, ``prompt`` and 0-5 ``accompaniment`` parts and check the target."""
    if not isinstance(upload.request, VoiceConversionGenerationRequest):  # pragma: no cover
        raise TypeError(upload.request)
    parts = {name: audio_parts(upload.form, name) for name in ("target", "prompt", "accompaniment")}
    _check_counts(parts)
    inputs, limit = context.files.inputs(generation_id), context.limits.stem_bytes
    saved = {
        name: await save_audio_parts(found, inputs, name, limit) for name, found in parts.items()
    }
    await _measure(context, saved["prompt"])
    _check_target_length(context, await _measure(context, saved["target"]))


def _check_target_length(context: ApiContext, seconds: float) -> None:
    maximum = context.settings.svc_max_target_seconds
    if seconds > maximum:
        raise invalid_input(TARGET_TOO_LONG_TEXT.format(limit=maximum, seconds=seconds))


def _check_counts(parts: dict[str, list[UploadFile]]) -> None:
    if len(parts["target"]) != 1:
        raise invalid_input(TARGET_COUNT_TEXT)
    if len(parts["prompt"]) != 1:
        raise invalid_input(PROMPT_COUNT_TEXT)
    if len(parts["accompaniment"]) > MAX_ACCOMPANIMENT:
        raise invalid_input(ACCOMPANIMENT_TEXT)


async def _measure(context: ApiContext, paths: list[Path]) -> float:
    try:
        return await run_in_threadpool(context.mixer.measure, paths)
    except InvalidInputError as error:
        raise invalid_input(str(error)) from error


INTAKES: Final[dict[str, Intake]] = {
    "restyle": restyle_intake, "song": song_intake, "voiceConversion": conversion_intake,
}  # fmt: skip
"""Intake per kind (the 1.1.0 kinds are added with their stories)."""
