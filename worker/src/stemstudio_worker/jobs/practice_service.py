"""Section, score-view and setlist document jobs on the ``practice`` lane (feature 014, ADR-023).

``queued -> running -> completed | failed | cancelled`` as the render jobs (007). A job's inputs
are checked when it is created: section stems against ``STEM_PATH_PATTERN`` (the file name is the
field's stem), details against ``DETAIL_PATH_PATTERN`` (path ``detail/<parent>/<detail>.flac`` of
channel ``<parent>.<detail>``), the score against ``SCORE_MUSICXML_PATTERN``; then the library's
path guard (400 / 404). The analyser and renderers are injected (composition in ``main.py``).
"""

import re
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path
from typing import Any, ClassVar, Final

from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lane_service import LaneJobService, Outcome
from stemstudio_worker.jobs.practice_lane import PracticeLane, PracticeParts
from stemstudio_worker.jobs.practice_store import (
    PracticeJobStore,
    StampedScoreViewJob,
    StampedSectionJob,
    StampedSetlistDocumentJob,
    StoredScoreViewJob,
    StoredSectionJob,
    StoredSetlistDocumentJob,
    wire_job,
)
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import (
    DETAIL_PATH_PATTERN,
    SCORE_MUSICXML_PATTERN,
    STEM_PATH_PATTERN,
    DocumentError,
    InputNotFoundError,
    InputPathError,
    LibraryReader,
    RenderError,
    ScoreView,
    ScoreViewError,
    ScoreViewRenderer,
    SectionProgress,
    SetlistDocument,
    SetlistDocumentRenderer,
    StemFiles,
    StructureAnalyzer,
    StructureError,
    WorkerError,
)
from stemstudio_worker.schemas import (
    RenderJobStatus,
    ScoreViewEngine,
    ScoreViewJob,
    ScoreViewJobRequest,
    ScoreViewPageInfo,
    ScoreViewResult,
    SectionJob,
    SectionJobRequest,
    SectionResult,
    SetlistDocumentJob,
    SetlistDocumentRequest,
    StructureAnalyzerName,
    TempoGrid,
)

_S = RenderJobStatus
MAX_MUSICXML_BYTES: Final = 32 * 1024 * 1024
"""Largest score MusicXML a score-view job reads."""
PDF_FILE: Final = "setlist.pdf"
NO_ANALYZER_TEXT: Final = "Section detection is not available."


class PracticeCancelledError(WorkerError):
    """A progress report of a deleted practice job (the job stops)."""


class NoAnalyzer:
    """Section analyser of a worker composed without one: every job fails readably."""

    name: StructureAnalyzerName = "stems-novelty"
    version: str = "1.0.0"

    def analyze(self, request: SectionJobRequest, files: StemFiles,
                progress: SectionProgress) -> SectionResult:  # fmt: skip
        """Fail: section detection is not available."""
        del request, files, progress
        raise StructureError(NO_ANALYZER_TEXT)


class NoScoreView:
    """Score-view renderer of a worker composed without one."""

    def render(self, musicxml: bytes, grid: TempoGrid, duration_seconds: float) -> ScoreView:
        """Fail with ``SCORE_VIEW_FAILED_TEXT``."""
        del musicxml, grid, duration_seconds
        raise ScoreViewError


class NoDocuments:
    """Setlist renderer of a worker composed without one."""

    def render(self, request: SetlistDocumentRequest) -> SetlistDocument:
        """Fail with ``DOCUMENT_FAILED_TEXT``."""
        del request
        raise DocumentError


@dataclass(frozen=True, slots=True)
class PracticeContext:
    """What every practice service shares: the lane (with the stores), the library, the clock."""

    lane: PracticeLane
    library: LibraryReader
    clock: Clock


class _PracticeService[R: Any](LaneJobService[R]):
    """Shared create/run/read logic; subclasses set ``WIRE`` and implement :meth:`run`."""

    LABEL: ClassVar[str] = "Practice"
    CANCELLED = PracticeCancelledError
    ERRORS = (StructureError, ScoreViewError, DocumentError, RenderError)
    WIRE: ClassVar[type[Any]]

    def __init__(self, context: PracticeContext, store: PracticeJobStore[R]) -> None:
        super().__init__(context.lane.scheduler, store, context.clock)
        self._library = context.library

    def get(self, job_id: str) -> Any:
        """The wire job with ``job_id``, or None."""
        record = self._store.get(job_id)
        return None if record is None else wire_job(record.job, self.WIRE)

    def process(self, job_id: str) -> None:
        """Run one job to a terminal state (lane thread); skips unknown and finished jobs."""
        record = self._pending(job_id)
        if record is None:
            return
        out_dir = self._store.output_dir(job_id)
        out_dir.mkdir(parents=True, exist_ok=True)
        self.update(job_id, status=_S.RUNNING)
        self._finish(job_id, self._guarded(job_id, lambda: self.run(record, out_dir)))

    def run(self, record: R, out_dir: Path) -> Outcome:
        """Do the work of ``record`` into ``out_dir``; return the completed job's changes."""
        raise NotImplementedError

    def _stamp(self) -> dict[str, Any]:
        now = self._clock()
        return {"job_id": new_id(), "status": _S.QUEUED, "progress": 0, "created_at": now,
                "updated_at": now}  # fmt: skip

    def _queue(self, record: R) -> Any:
        self._store.save(record)
        self._submit(record.job.job_id)
        return wire_job(record.job, self.WIRE)

    def _reporter(self, job_id: str) -> Callable[[float], None]:
        last = [0.0]

        def report(fraction: float, /) -> None:
            last[0] = max(last[0], min(max(fraction, 0.0), 1.0))
            if not self.update(job_id, progress=last[0]):
                raise PracticeCancelledError("The practice job was deleted")

        return report

    def _resolve(self, path: str) -> Path:
        """The library file of an already checked ``path`` (``RenderError`` without a library)."""
        return self._library.resolve(path)


def check_input(library: LibraryReader, path: str, pattern: re.Pattern[str]) -> None:
    """Require ``pattern`` and pass the library's path guard.

    Raises:
        InputPathError: Not the pattern, not on the allow-list or outside the root (400).
        InputNotFoundError: The file does not exist (404).

    A library that is not readable now is not an error here: the job fails when it runs.
    """
    if not pattern.fullmatch(path):
        raise InputPathError(f"Input path is not allowed: {path}")
    try:
        library.resolve(path)
    except (InputPathError, InputNotFoundError):
        raise
    except RenderError:
        return


class SectionService(_PracticeService[StoredSectionJob]):
    """Section jobs (``POST /v1/section-jobs``)."""

    LABEL = "Section"
    WIRE = SectionJob

    def __init__(self, context: PracticeContext, analyzer: StructureAnalyzer) -> None:
        """Serve the section store of ``context`` with ``analyzer``."""
        super().__init__(context, context.lane.stores.sections)
        self._analyzer = analyzer

    def create(self, request: SectionJobRequest) -> SectionJob:
        """Check the stem and detail paths and queue the job (400 / 404 as ``check_input``)."""
        for path in section_inputs(request).values():
            check_input(self._library, path, _pattern_of(path))
        job = StampedSectionJob(**self._stamp())
        created: SectionJob = self._queue(StoredSectionJob(job=job, request=request))
        return created

    def run(self, record: StoredSectionJob, out_dir: Path) -> Outcome:  # noqa: ARG002
        """Resolve the files and analyse them."""
        files = {name: self._resolve(path) for name, path in section_inputs(record.request).items()}
        result = self._analyzer.analyze(record.request, files, self._reporter(record.job.job_id))
        return {"status": _S.COMPLETED, "progress": 1.0, "result": result}


def section_inputs(request: SectionJobRequest) -> dict[str, str]:
    """Channel -> library path of every stem and detail of ``request``.

    Raises:
        InputPathError: A stem path names another stem, or a detail path another channel.
    """
    stems = request.stems.model_dump(exclude_none=True)
    inputs = {name: _named(path, f"/stems/{name}.flac") for name, path in stems.items()}
    for detail in request.details:
        parent, _, name = detail.channel.partition(".")
        inputs[detail.channel] = _named(detail.path, f"/detail/{parent}/{name}.flac")
    return inputs


def _named(path: str, suffix: str) -> str:
    if not path.endswith(suffix):
        raise InputPathError(f"Input path is not allowed: {path}")
    return path


def _pattern_of(path: str) -> re.Pattern[str]:
    return DETAIL_PATH_PATTERN if "/detail/" in path else STEM_PATH_PATTERN


class ScoreViewService(_PracticeService[StoredScoreViewJob]):
    """Score-view jobs (``POST /v1/score-view-jobs``) and their SVG pages."""

    LABEL = "Score view"
    WIRE = ScoreViewJob

    def __init__(self, context: PracticeContext, renderer: ScoreViewRenderer) -> None:
        """Serve the score-view store of ``context`` with ``renderer``."""
        super().__init__(context, context.lane.stores.score_views)
        self._renderer = renderer

    def create(self, request: ScoreViewJobRequest) -> ScoreViewJob:
        """Check the MusicXML path (``assets/<uuid>/score.musicxml`` only) and queue the job."""
        check_input(self._library, request.musicxml, SCORE_MUSICXML_PATTERN)
        job = StampedScoreViewJob(**self._stamp())
        created: ScoreViewJob = self._queue(StoredScoreViewJob(job=job, request=request))
        return created

    def page(self, job_id: str, page: int) -> Path | None:
        """The SVG file of page ``page`` (1-based) of a job, or None."""
        path = self._store.output_dir(job_id) / page_file(page)
        return path if path.is_file() else None

    def run(self, record: StoredScoreViewJob, out_dir: Path) -> Outcome:
        """Read the score, lay it out and store the pages."""
        musicxml = _read_limited(self._resolve(record.request.musicxml))
        request = record.request
        view = self._renderer.render(musicxml, request.grid, request.duration_seconds)
        for number, svg in enumerate(view.pages, 1):
            (out_dir / page_file(number)).write_text(svg, encoding="utf-8")
        return {"status": _S.COMPLETED, "progress": 1.0, "result": view_result(view)}


def page_file(page: int) -> str:
    """``page-001.svg`` ... (data-model 2.5 step 5)."""
    return f"page-{page:03d}.svg"


def _read_limited(path: Path) -> bytes:
    if path.stat().st_size > MAX_MUSICXML_BYTES:
        raise ScoreViewError
    return path.read_bytes()


def view_result(view: ScoreView) -> ScoreViewResult:
    """The contract result of a laid-out score."""
    pages = [ScoreViewPageInfo(page=n, width_px=view.page_width_px, height_px=view.page_height_px)
             for n in range(1, len(view.pages) + 1)]  # fmt: skip
    return ScoreViewResult(pages=pages, bars=list(view.bars),
                           engine=ScoreViewEngine(version=view.engine_version))  # fmt: skip


class SetlistDocumentService(_PracticeService[StoredSetlistDocumentJob]):
    """Setlist PDF jobs (``POST /v1/setlist-document-jobs``)."""

    LABEL = "Setlist document"
    WIRE = SetlistDocumentJob

    def __init__(self, context: PracticeContext, renderer: SetlistDocumentRenderer) -> None:
        """Serve the setlist store of ``context`` with ``renderer``."""
        super().__init__(context, context.lane.stores.documents)
        self._renderer = renderer

    def create(self, request: SetlistDocumentRequest) -> SetlistDocumentJob:
        """Queue the job (the content is inline; nothing to check beyond the schema)."""
        job = StampedSetlistDocumentJob(**self._stamp())
        record = StoredSetlistDocumentJob(job=job, request=request)
        created: SetlistDocumentJob = self._queue(record)
        return created

    def file(self, job_id: str) -> Path | None:
        """The PDF of a job, or None."""
        path = self._store.output_dir(job_id) / PDF_FILE
        return path if path.is_file() else None

    def run(self, record: StoredSetlistDocumentJob, out_dir: Path) -> Outcome:
        """Render the PDF."""
        document: SetlistDocument = self._renderer.render(record.request)
        (out_dir / PDF_FILE).write_bytes(document.pdf)
        return {"status": _S.COMPLETED, "progress": 1.0, "pages": document.pages}


@dataclass(frozen=True, slots=True)
class PracticeServices:
    """The ``practice`` lane and its three job services (``Lanes.practice``)."""

    lane: PracticeLane
    parts: PracticeParts
    sections: SectionService
    score_views: ScoreViewService
    documents: SetlistDocumentService

    @classmethod
    def start(cls, context: PracticeContext, parts: PracticeParts) -> "PracticeServices":
        """Build the services over the (loaded, reconciled) lane and start it."""
        sections = SectionService(context, parts.analyzer or NoAnalyzer())
        views = ScoreViewService(context, parts.score_views or NoScoreView())
        documents = SetlistDocumentService(context, parts.documents or NoDocuments())
        context.lane.start()
        return cls(context.lane, parts, sections, views, documents)
