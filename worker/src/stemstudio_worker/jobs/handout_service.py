"""Handout jobs on the ``insights`` lane (feature 020, data-model 2.5, ``POST /v1/handout-jobs``).

Every artwork path must match ``ports.ARTWORK_PATTERN`` and every notation path
``ports.HANDOUT_PDF_PATTERN`` (the per-job allow-list of data-model 2.2, per field; never stems or
MusicXML), and each must resolve to a regular file inside the real library root (400 / 404 when
the job is created). When
the job runs the paths are resolved again and the renderer gets their real paths as ``files``;
it reads nothing else but its packaged assets. Stages: ``layout`` 0-0.6, ``notation`` 0.6-0.9,
``writing`` 0.9-1.0. The PDF is ``handout-jobs/<id>/handout.pdf``.
"""

import functools
import hashlib
import math
import re
from pathlib import Path
from typing import Final, cast

from stemstudio_worker.jobs.handout_store import StoredHandoutJob
from stemstudio_worker.jobs.input_guard import check_input, resolve_contained
from stemstudio_worker.jobs.insights_service import InsightsContext, InsightsJobService, stage_in
from stemstudio_worker.jobs.lane_service import Outcome
from stemstudio_worker.ports import (
    ARTWORK_PATTERN,
    HANDOUT_FAILED_TEXT,
    HANDOUT_PDF_PATTERN,
    HandoutCancelledError,
    HandoutRenderer,
    HandoutRenderError,
    InsightsProgress,
    RenderedHandout,
    RenderError,
)
from stemstudio_worker.schemas import (
    HandoutJob,
    HandoutJobRequest,
    HandoutResult,
    HandoutStage,
    RenderJobStatus,
)

HANDOUT_STAGES: Final = ((0.6, "layout"), (0.9, "notation"), (math.inf, "writing"))


def stage_of(fraction: float) -> HandoutStage:
    """The contract stage of a handout job at ``fraction`` (0..1)."""
    return cast(HandoutStage, stage_in(HANDOUT_STAGES, fraction))


def field_paths(request: HandoutJobRequest) -> list[tuple[str, re.Pattern[str]]]:
    """Every input path with the pattern of its field: artwork covers, notation score PDFs."""
    pairs: list[tuple[str, re.Pattern[str]]] = []
    for song in request.document.songs:
        if song.artwork_path is not None:
            pairs.append((song.artwork_path, ARTWORK_PATTERN))
        pairs.extend((item.pdf_path, HANDOUT_PDF_PATTERN) for item in song.notation)
    return pairs


class UnavailableRenderer:
    """Stands in for a worker composed without a renderer: every job fails readably."""

    def render(self, request: HandoutJobRequest, files: object,
               progress: InsightsProgress) -> RenderedHandout:  # fmt: skip
        """Fail with ``HANDOUT_FAILED_TEXT``."""
        del request, files, progress
        raise HandoutRenderError(HANDOUT_FAILED_TEXT)


class HandoutService(InsightsJobService[StoredHandoutJob]):
    """Handout jobs (``POST /v1/handout-jobs``) and their PDFs."""

    LABEL = "Handout"
    CANCELLED = HandoutCancelledError
    ERRORS = (HandoutRenderError, RenderError)
    ID_FIELD = "handout_job_id"
    STAGES = HANDOUT_STAGES

    def __init__(self, context: InsightsContext, renderer: HandoutRenderer) -> None:
        """Serve the handout store of ``context`` with ``renderer``."""
        super().__init__(context, context.lane.stores.handouts)
        self._renderer = renderer
        self._root = context.library_root
        self._handouts = context.lane.stores.handouts

    def create(self, request: HandoutJobRequest) -> HandoutJob:
        """Check every notation and artwork path (400 / 404) and queue the job."""
        resolve = functools.partial(resolve_contained, self._root)
        for path, pattern in field_paths(request):
            check_input(resolve, path, pattern.fullmatch)
        job = HandoutJob(**self._stamp())
        created: HandoutJob = self._queue(StoredHandoutJob(job=job, request=request))
        return created

    def file(self, job_id: str) -> Path | None:
        """The PDF of a job, or None."""
        path = self._handouts.pdf_path(job_id)
        return path if path.is_file() else None

    def run(self, record: StoredHandoutJob, out_dir: Path) -> Outcome:  # noqa: ARG002
        """Resolve the inputs, render the PDF and store it with its facts."""
        report = self._reporter(record.job.handout_job_id)
        report(0.0)
        files = {path: self._resolve(path) for path in record.request.input_paths()}
        rendered = self._render(record.request, files, report)
        pdf = rendered.pdf
        self._handouts.pdf_path(record.job.handout_job_id).write_bytes(pdf)
        result = HandoutResult(size_bytes=len(pdf), sha256=hashlib.sha256(pdf).hexdigest(),
                               pages=rendered.pages, outline=list(rendered.outline),
                               warnings=list(rendered.warnings))  # fmt: skip
        return {"status": RenderJobStatus.COMPLETED, "progress": 1.0, "result": result}

    def _resolve(self, path: str) -> Path:
        """The real file of a path checked at creation (the stored request is never changed)."""
        return resolve_contained(self._root, path)

    def _render(self, request: HandoutJobRequest, files: dict[str, Path],
                report: InsightsProgress) -> RenderedHandout:  # fmt: skip
        """Render; a render error without a text gets ``HANDOUT_FAILED_TEXT``."""
        try:
            return self._renderer.render(request, files, report)
        except HandoutCancelledError:
            raise
        except HandoutRenderError as exc:
            raise HandoutRenderError(str(exc) or HANDOUT_FAILED_TEXT) from exc
