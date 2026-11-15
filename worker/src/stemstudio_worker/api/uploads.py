"""Multipart upload intake: type check, streamed size limit, storage under ``uploads/<jobId>/``.

The request body is counted while it streams into the multipart parser, so an oversized upload
is rejected with 413 as soon as the limit is passed, also without a ``Content-Length`` header
(chunked transfer). Nothing beyond the limit plus a small multipart allowance is ever read.
"""

import shutil
from collections.abc import AsyncGenerator, AsyncIterator
from contextlib import aclosing
from pathlib import Path, PureWindowsPath

from fastapi import Request
from starlette.concurrency import run_in_threadpool
from starlette.datastructures import FormData, UploadFile
from starlette.formparsers import MultiPartException, MultiPartParser

from stemstudio_worker.api.problems import ProblemError
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
_MULTIPART_OVERHEAD_BYTES = 64 * 1024
_UNSUPPORTED = "Unsupported file type"
_INVALID = "Invalid request"
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
    _check_content_length(request, limit)
    form = await _parse_form(request, limit)
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


async def _parse_form(request: Request, limit: int) -> FormData:
    """Parse the multipart body from a size-limited stream (malformed multipart is a 400)."""
    async with aclosing(_limited(request.stream(), limit)) as stream:
        parser = MultiPartParser(request.headers, stream, max_files=1, max_fields=10)
        try:
            return await parser.parse()
        except MultiPartException as exc:
            raise ProblemError(400, _INVALID, exc.message) from exc


async def _limited(chunks: AsyncIterator[bytes], limit: int) -> AsyncGenerator[bytes, None]:
    """Pass ``chunks`` through; raise 413 once they exceed ``limit`` + the multipart allowance."""
    received = 0
    async for chunk in chunks:
        received += len(chunk)
        if received > limit + _MULTIPART_OVERHEAD_BYTES:
            raise _too_large(limit)
        yield chunk


def _check_content_length(request: Request, limit: int) -> None:
    """Reject a request whose declared size cannot fit the limit before reading the body."""
    declared = request.headers.get("content-length", "")
    if declared.isdigit() and int(declared) > limit + _MULTIPART_OVERHEAD_BYTES:
        raise _too_large(limit)


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
