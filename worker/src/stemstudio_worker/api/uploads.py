"""Multipart upload intake: type check, streamed size limit, storage under ``uploads/<jobId>/``.

The size limit is enforced while the body streams (``api.multipart``).
"""

import shutil
from pathlib import Path, PureWindowsPath

from fastapi import Request
from starlette.concurrency import run_in_threadpool
from starlette.datastructures import UploadFile

from stemstudio_worker.api.multipart import INVALID, parse_limited_form
from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.jobs.artwork import stage_upload_artwork
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    EmptyUploadError,
    InvalidSourceError,
    UnsupportedMediaError,
    UploadTooLargeError,
)
from stemstudio_worker.schemas import Job

ALLOWED_EXTENSIONS = (".mp3", ".wav", ".flac")
_CHUNK_BYTES = 1024 * 1024
_UNSUPPORTED = "Unsupported file type"
_INVALID = INVALID
_TOO_LARGE = "File too large"
_INTAKE_STATUS: tuple[tuple[type[InvalidSourceError], int, str], ...] = (
    (UploadTooLargeError, 413, _TOO_LARGE),
    (UnsupportedMediaError, 415, _UNSUPPORTED),
    (EmptyUploadError, 400, _INVALID),
)


async def create_from_upload(request: Request, runtime: WorkerRuntime) -> tuple[Job, bool]:
    """Store the ``file`` part and create (or reuse) its job.

    Raises:
        ProblemError: 400 without a ``file`` part, for malformed multipart or an empty file;
            413 over the size limit; 415 for a type other than MP3, WAV or FLAC.
    """
    limit = runtime.settings.max_upload_bytes
    form = await parse_limited_form(request, limit, _too_large(limit))
    try:
        upload = form.get("file")
        if not isinstance(upload, UploadFile):
            raise ProblemError(400, _INVALID, "Send the audio as multipart part 'file'.")
        return await _store_and_register(runtime, upload, limit)
    finally:
        await form.close()


async def _store_and_register(
    runtime: WorkerRuntime, upload: UploadFile, limit: int
) -> tuple[Job, bool]:
    """Copy the part to ``uploads/<new jobId>/input.<ext>`` and register it; clean up on error."""
    file_name = upload.filename or ""
    target = runtime.paths.uploads(JobStore.new_job_id()) / f"input{_extension(file_name)}"
    try:
        await _save(upload, target, limit)
        return await run_in_threadpool(_register, runtime, file_name, target)
    except Exception:
        shutil.rmtree(target.parent, ignore_errors=True)
        raise


def _extension(file_name: str) -> str:
    """Return the lower-case extension of the client file name (also for Windows paths)."""
    suffix = PureWindowsPath(file_name).suffix.lower()
    if suffix not in ALLOWED_EXTENSIONS:
        raise ProblemError(415, _UNSUPPORTED, "Only MP3, WAV and FLAC files are supported.")
    return suffix


async def _save(upload: UploadFile, target: Path, limit: int) -> None:
    """Copy the upload to ``target`` in chunks, stopping as soon as it exceeds ``limit``."""
    target.parent.mkdir(parents=True, exist_ok=True)
    size = 0
    with target.open("wb") as out:
        while chunk := await upload.read(_CHUNK_BYTES):
            size += len(chunk)
            if size > limit:
                raise _too_large(limit)
            out.write(chunk)
    if size == 0:
        raise ProblemError(400, _INVALID, "The file is empty.")


def _register(runtime: WorkerRuntime, file_name: str, target: Path) -> tuple[Job, bool]:
    """Validate the stored upload and create its job; a duplicate discards the stored copy."""
    try:
        source = runtime.adapters.ingest.upload_intake.accept(file_name, target)
    except InvalidSourceError as exc:
        raise _intake_problem(exc) from exc
    stage_upload_artwork(target.parent, source)
    job, created = runtime.service.create(
        source, job_id=target.parent.name, upload_file=target.name
    )
    if not created:
        shutil.rmtree(target.parent, ignore_errors=True)
    return job, created


def _intake_problem(exc: InvalidSourceError) -> ProblemError:
    """Map an intake rejection to its contract status: 413, 415, else 400."""
    status, title = next(
        ((status, title) for kind, status, title in _INTAKE_STATUS if isinstance(exc, kind)),
        (400, "Invalid source"),
    )
    return ProblemError(status, title, str(exc))


def _too_large(limit: int) -> ProblemError:
    mib = limit / (1024 * 1024)
    return ProblemError(413, _TOO_LARGE, f"The file is larger than {mib:g} MiB ({limit:,} bytes).")
