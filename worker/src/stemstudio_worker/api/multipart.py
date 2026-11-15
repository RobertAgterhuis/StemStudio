"""Size-limited multipart parsing shared by the audio and picture uploads.

The request body is counted while it streams into the multipart parser, so an oversized upload
is rejected with 413 as soon as the limit is passed, also without a ``Content-Length`` header
(chunked transfer). Nothing beyond the limit plus a small multipart allowance is ever read.
"""

from collections.abc import AsyncGenerator, AsyncIterator
from contextlib import aclosing

from fastapi import Request
from starlette.datastructures import FormData
from starlette.formparsers import MultiPartException, MultiPartParser

from stemstudio_worker.api.problems import ProblemError

MULTIPART_OVERHEAD_BYTES = 64 * 1024
"""Allowance for boundaries and part headers on top of the file size limit."""

INVALID = "Invalid request"


async def parse_limited_form(request: Request, limit: int, too_large: ProblemError) -> FormData:
    """Parse a multipart body of at most ``limit`` file bytes (plus the allowance).

    Raises:
        ProblemError: ``too_large`` when the body (or its declared length) exceeds the limit;
            400 for malformed multipart.
    """
    declared = request.headers.get("content-length", "")
    if declared.isdigit() and int(declared) > limit + MULTIPART_OVERHEAD_BYTES:
        raise too_large
    async with aclosing(_limited(request.stream(), limit, too_large)) as stream:
        parser = MultiPartParser(request.headers, stream, max_files=1, max_fields=10)
        try:
            return await parser.parse()
        except MultiPartException as exc:
            raise ProblemError(400, INVALID, exc.message) from exc


async def _limited(
    chunks: AsyncIterator[bytes], limit: int, too_large: ProblemError
) -> AsyncGenerator[bytes, None]:
    """Pass ``chunks`` through; raise ``too_large`` once they exceed the limit + allowance."""
    received = 0
    async for chunk in chunks:
        received += len(chunk)
        if received > limit + MULTIPART_OVERHEAD_BYTES:
            raise too_large
        yield chunk
