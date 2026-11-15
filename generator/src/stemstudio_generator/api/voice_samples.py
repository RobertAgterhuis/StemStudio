"""Routes of the voice-sample checks (1.1.0, singer service; CPU only, no GPU lease)."""

import re
from pathlib import Path
from typing import Final

from fastapi import APIRouter, Request
from fastapi.responses import FileResponse, JSONResponse
from starlette.background import BackgroundTask
from starlette.concurrency import run_in_threadpool
from starlette.datastructures import UploadFile

from stemstudio_generator.api.context import ApiContext
from stemstudio_generator.api.problems import (
    invalid_input,
    not_found,
    problem_error,
    too_large,
)
from stemstudio_generator.api.samples import CheckedSamples
from stemstudio_generator.api.uploads import size_text
from stemstudio_generator.config import (
    SAMPLE_MAX_SECONDS,
    SAMPLE_MIN_SECONDS,
    SAMPLE_MIN_VOICED_RATIO,
)
from stemstudio_generator.ports import InvalidInputError, SampleFacts
from stemstudio_generator.schemas import KIND_NOT_SERVED_TITLE, VoiceSampleCheck

UNREADABLE_TEXT: Final = "The sample could not be read as audio."
NOT_CHECKING_TEXT: Final = "This service does not check voice samples."
TOO_SHORT_TEXT: Final = (
    "The recording is {seconds:.1f} s long; at least 10 s is needed. Sing for 20-30 seconds."
)
TOO_LONG_TEXT: Final = (
    "The recording is {seconds:.1f} s long; at most 60 s is allowed. Keep it to 20-30 seconds."
)
UNVOICED_TEXT: Final = (
    "The recording contains too little singing ({percent} % voiced, at least 50 % is needed). "
    "Sing for 20-30 seconds without background music."
)
_ID: Final = re.compile(r"^[0-9a-f]{32}$")

router = APIRouter()


def _context(request: Request) -> ApiContext:
    context: ApiContext = request.app.state.context
    if context.analyzer is None or context.samples is None:
        raise problem_error(409, KIND_NOT_SERVED_TITLE, NOT_CHECKING_TEXT, "kindNotServed")
    return context


@router.post("/v1/voice-samples/check")
async def check_voice_sample(request: Request) -> JSONResponse:
    """Decode, normalise and measure a sample; keep it for 10 minutes when ``ok``."""
    context = _context(request)
    form = await request.form(max_files=1, max_fields=2)
    try:
        sample = form.get("sample")
        if not isinstance(sample, UploadFile):
            raise invalid_input(UNREADABLE_TEXT)
        check = await _check(context, sample)
    finally:
        await form.close()
    return JSONResponse(check.model_dump(mode="json", by_alias=True))


@router.get("/v1/voice-samples/{check_id}/audio")
def get_checked_voice_sample(request: Request, check_id: str) -> FileResponse:
    """The normalised sample, once; deleted when the response completes."""
    context = _context(request)
    path = context.samples.take(check_id) if _ID.match(check_id) and context.samples else None
    if path is None:
        raise not_found(f"Voice sample check {check_id} has no sample to download.")
    return FileResponse(
        path, media_type="audio/flac", background=BackgroundTask(path.unlink, missing_ok=True)
    )


async def _check(context: ApiContext, sample: UploadFile) -> VoiceSampleCheck:
    limit = context.limits.sample_bytes
    if sample.size is not None and sample.size > limit:
        raise too_large(f"The sample is larger than {size_text(limit)}.")
    samples = _samples(context)
    check_id = context.new_id()
    dest = samples.path_for(check_id)
    check = judge(check_id, await _analyze(context, sample, dest))
    if check.ok:
        samples.keep(check_id)
    else:
        dest.unlink(missing_ok=True)
    return check


async def _analyze(context: ApiContext, sample: UploadFile, dest: Path) -> SampleFacts:
    """Save the upload next to ``dest``, analyse it, always delete the upload copy."""
    analyzer = context.analyzer
    if analyzer is None:  # pragma: no cover - checked by _context
        raise TypeError(analyzer)
    upload = dest.with_suffix(".upload")
    upload.write_bytes(await sample.read())
    try:
        return await run_in_threadpool(analyzer.analyze, upload, dest)
    except InvalidInputError as error:
        dest.unlink(missing_ok=True)
        raise invalid_input(UNREADABLE_TEXT) from error
    finally:
        upload.unlink(missing_ok=True)


def _samples(context: ApiContext) -> CheckedSamples:
    if context.samples is None:  # pragma: no cover - checked by _context
        raise TypeError(context.samples)
    return context.samples


def judge(check_id: str, facts: SampleFacts) -> VoiceSampleCheck:
    """``ok`` iff 10 <= duration <= 60 s and voiced ratio >= 0.5; else the first issue."""
    seconds, ratio = round(facts.duration_seconds, 3), round(facts.voiced_ratio, 3)
    base = {
        "check_id": check_id,
        "duration_seconds": seconds,
        "voiced_ratio": ratio,
        "pcm_sha256": facts.pcm_sha256,
    }
    issue = _issue(seconds, ratio)
    return VoiceSampleCheck.model_validate({**base, "ok": issue is None, **(issue or {})})


def _issue(seconds: float, ratio: float) -> dict[str, str] | None:
    """The first problem of a sample (duration before voicing), or None."""
    if seconds < SAMPLE_MIN_SECONDS:
        return {"reason": "tooShort", "message": TOO_SHORT_TEXT.format(seconds=seconds)}
    if seconds > SAMPLE_MAX_SECONDS:
        return {"reason": "tooLong", "message": TOO_LONG_TEXT.format(seconds=seconds)}
    if ratio < SAMPLE_MIN_VOICED_RATIO:
        return {"reason": "unvoiced", "message": UNVOICED_TEXT.format(percent=round(ratio * 100))}
    return None
