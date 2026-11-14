"""Multipart upload intake: type check, streamed size limit, storage under ``uploads/<jobId>/``."""

import shutil
from pathlib import Path, PureWindowsPath

from fastapi import Request
from starlette.concurrency import run_in_threadpool
from starlette.datastructures import UploadFile

from stemstudio_worker.api.problems import ProblemError
from stemstudio_worker.jobs.runtime import WorkerRuntime
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import InvalidSourceError
from stemstudio_worker.schemas import Job

ALLOWED_EXTENSIONS = (".mp3", ".wav", ".flac")
_CHUNK_BYTES = 1024 * 1024
_MULTIPART_OVERHEAD_BYTES = 64 * 1024
_UNSUPPORTED = "Unsupported file type"


async def create_from_upload(request: Request, runtime: WorkerRuntime) -> tuple[Job, bool]:
    """Store the ``file`` part and create (or reuse) its job.

    Raises:
        ProblemError: 400 without a ``file`` part or for an empty file, 413 over the size limit,
            415 for a type other than MP3, WAV or FLAC.
    """
    limit = runtime.settings.max_upload_bytes
    _check_content_length(request, limit)
    async with request.form(max_files=1, max_fields=10) as form:
        upload = form.get("file")
        if not isinstance(upload, UploadFile):
            raise ProblemError(400, "Invalid request", "Send the audio as multipart part 'file'.")
        file_name = upload.filename or ""
        target = runtime.paths.uploads(JobStore.new_job_id()) / f"input{_extension(file_name)}"
        try:
            await _save(upload, target, limit)
            return await run_in_threadpool(_register, runtime, file_name, target)
        except Exception:
            shutil.rmtree(target.parent, ignore_errors=True)
            raise


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
        raise ProblemError(400, "Invalid request", "The file is empty.")


def _register(runtime: WorkerRuntime, file_name: str, target: Path) -> tuple[Job, bool]:
    """Validate the stored upload and create its job; a duplicate discards the stored copy."""
    try:
        source = runtime.adapters.ingest.upload_intake.accept(file_name, target)
    except InvalidSourceError as exc:
        raise ProblemError(415, _UNSUPPORTED, str(exc)) from exc
    job, created = runtime.service.create(
        source, job_id=target.parent.name, upload_file=target.name
    )
    if not created:
        shutil.rmtree(target.parent, ignore_errors=True)
    return job, created


def _too_large(limit: int) -> ProblemError:
    mib = limit / (1024 * 1024)
    return ProblemError(
        413, "File too large", f"The file is larger than {mib:g} MiB ({limit:,} bytes)."
    )
