"""Per-kind intake of ``createGeneration``: save and check the uploaded files of a new job.

Each intake runs after the request was validated, the kind is served and the GPU is ready. It
saves the parts into the job's ``inputs/`` directory and refuses unusable audio before the job
is queued (``400``/``413``/``415``/``422``).
"""

from collections.abc import Awaitable, Callable
from typing import Final

from starlette.concurrency import run_in_threadpool

from stemstudio_generator.api.context import ApiContext
from stemstudio_generator.api.problems import invalid_input, source_too_short
from stemstudio_generator.api.uploads import GenerationUpload, audio_parts, save_audio_parts
from stemstudio_generator.ports import InvalidInputError
from stemstudio_generator.schemas import RestyleGenerationRequest

Intake = Callable[[ApiContext, GenerationUpload, str], Awaitable[None]]
"""``(context, upload, generation_id)``: save and check the job's inputs."""

_RANGE_TOLERANCE_SECONDS: Final = 1e-3


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


INTAKES: Final[dict[str, Intake]] = {"restyle": restyle_intake}
"""Intake per kind (the 1.1.0 kinds are added with their stories)."""
