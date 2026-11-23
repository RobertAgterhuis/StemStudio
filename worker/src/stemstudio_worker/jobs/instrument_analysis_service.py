"""Instrument analysis jobs on the ``instrument-analysis`` lane (feature 016, DELTA 1, ADR-031).

``queued -> running -> completed | failed | cancelled`` as the render jobs (007), one job at a time
on a lane of its own (never behind ``separate``, ``render``, ``transcribe`` or ``practice``). Every
input passes the library's path guard when the job is created (400 / 404); a library that cannot
be read then fails the job when it runs. Kinds:

* ``chords``: the ``ChordAnalyzer`` (mixdown -> recognizer -> post-processing -> key) over the
  resolved harmonic stems and the bass stem; the device of a BTC run is recorded.
* ``drum-hits``: the 008 ``DrumTranscriber`` per given piece -> hits in time order (ms);
  hits in the same millisecond keep the kit order kick, snare, toms, hihat, cymbals. Above
  ``MAX_DRUM_HITS`` every piece keeps its share of the cap (largest remainder) with its strongest
  hits (ties: the earlier), so the whole song stays covered; the cap is logged as a warning
  (``DrumHits`` 1.9.0 has no warnings field).
* ``vocal-pitch``: the vocal stem at 16 kHz -> the ``PitchAnalyzer`` (``NoMelodyError`` fails
  the job with ``NO_MELODY_TEXT``).

Stages follow the progress: ``preparing`` below 0.05 (``loading-model`` when the BTC weights of
the request are not cached yet), ``analysing`` below 0.95, then ``finishing``.
"""

import heapq
import logging
from typing import Any, ClassVar, Final, get_args

from stemstudio_worker.jobs.instrument_analysis_chords import MIXED, scaled
from stemstudio_worker.jobs.instrument_analysis_ports import ChordInputs, InstrumentAnalysisParts
from stemstudio_worker.jobs.instrument_analysis_store import (
    InstrumentAnalysisStore,
    StoredAnalysisJob,
)
from stemstudio_worker.jobs.json_store import new_id
from stemstudio_worker.jobs.lane_service import LaneJobService, Outcome
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import Clock
from stemstudio_worker.ports import (
    PITCH_SAMPLE_RATE,
    AnalysisCancelledError,
    AnalysisError,
    AnalysisProgress,
    InputNotFoundError,
    InputPathError,
    LibraryReader,
    RenderError,
    TranscriptionError,
    merge_device,
)
from stemstudio_worker.schemas import (
    MAX_DRUM_HITS,
    AnyAnalysisRequest,
    ChordsJobRequest,
    DrumHitEvent,
    DrumHits,
    DrumHitsJobRequest,
    DrumPiece,
    InstrumentAnalysisHealth,
    InstrumentAnalysisJob,
    InstrumentAnalysisStage,
    RenderJobStatus,
    VocalPitchJobRequest,
)

_LOG = logging.getLogger(__name__)
_S = RenderJobStatus
FINISHING: Final = 0.95
"""Progress from which a job is ``finishing``."""

_PITCH_LOADED: Final = 0.1
_MODEL_IDS: Final = {"simple": "btc_majmin", "full": "btc_large_voca"}
_KIT: Final[tuple[DrumPiece, ...]] = get_args(DrumPiece)
"""Order of simultaneous hits (as the 008 ``DrumTranscriber``)."""


class InstrumentAnalysisService(LaneJobService[StoredAnalysisJob]):
    """``POST``/``GET``/``DELETE /v1/instrument-analysis-jobs`` over one lane and one store."""

    LABEL: ClassVar[str] = "Instrument analysis"
    CANCELLED = AnalysisCancelledError
    ERRORS = (AnalysisError, TranscriptionError, RenderError, InputPathError, InputNotFoundError)

    def __init__(
        self, store: InstrumentAnalysisStore, parts: InstrumentAnalysisParts, context: "Context"
    ) -> None:
        """Serve ``store`` with ``parts``; ``context`` gives the library and the clock."""
        super().__init__("instrument-analysis", store, context.clock)
        self.parts = parts
        self._library = context.library

    @classmethod
    def start(
        cls, paths: DataPaths, parts: InstrumentAnalysisParts, context: "Context"
    ) -> "InstrumentAnalysisService":
        """Load the jobs, fail the ones a restart interrupted and start the lane."""
        store = InstrumentAnalysisStore.loaded(paths, context.clock)
        service = cls(store, parts, context)
        service._lane.start()
        return service

    def create(self, request: AnyAnalysisRequest) -> InstrumentAnalysisJob:
        """Check the input paths and queue the job (400 / 404 from the path guard)."""
        for path in request.input_paths():
            _check(self._library, path)
        now = self._clock()
        job = InstrumentAnalysisJob(analysis_job_id=new_id(), kind=request.kind, status=_S.QUEUED,
                                    progress=0, created_at=now, updated_at=now)  # fmt: skip
        self._store.save(StoredAnalysisJob(job=job, request=request))
        self._submit(job.analysis_job_id)
        return job

    def get(self, job_id: str) -> InstrumentAnalysisJob | None:
        """The job with ``job_id``, or None."""
        job: InstrumentAnalysisJob | None = self.job_of(job_id)
        return job

    def health(self) -> InstrumentAnalysisHealth:
        """``Health.instrumentAnalysis``: BTC readiness and the configured engines."""
        chords = self.parts.chords
        return InstrumentAnalysisHealth(models=chords.models(), chord_engine=chords.default_engine,
                                        pitch_engine=self.parts.pitch_engine)  # fmt: skip

    def process(self, job_id: str) -> None:
        """Run one job to a terminal state (lane thread); skips unknown and finished jobs."""
        record = self._pending(job_id)
        if record is None:
            return
        first = self._first_stage(record.request)
        self.update(job_id, status=_S.RUNNING, stage=first)
        report = _Reporter(self, job_id, first)
        outcome = self._guarded(job_id, lambda: self._run(record.request, report))
        self._finish(job_id, {**outcome, "stage": None})

    def _first_stage(self, request: AnyAnalysisRequest) -> InstrumentAnalysisStage:
        """``loading-model`` for a BTC chords job without cached weights, else ``preparing``."""
        if not isinstance(request, ChordsJobRequest):
            return "preparing"
        chords = self.parts.chords
        if (request.engine or chords.default_engine) != "btc":
            return "preparing"
        ready = getattr(chords.models(), _MODEL_IDS[request.vocabulary])
        return "preparing" if ready == "ready" else "loading-model"

    def _run(self, request: AnyAnalysisRequest, report: AnalysisProgress) -> Outcome:
        if isinstance(request, ChordsJobRequest):
            return self._chords(request, report)
        if isinstance(request, DrumHitsJobRequest):
            return self._drum_hits(request, report)
        return self._vocal_pitch(request, report)

    def _chords(self, request: ChordsJobRequest, report: AnalysisProgress) -> Outcome:
        harmonic = tuple(self._library.resolve(path) for path in request.inputs)
        bass = None if request.bass_input is None else self._library.resolve(request.bass_input)
        outcome = self.parts.chords.analyze(request, ChordInputs(harmonic, bass), report)
        device = None if outcome.device is None else merge_device(None, outcome.device)
        return _completed(outcome.chart, device=device)

    def _drum_hits(self, request: DrumHitsJobRequest, report: AnalysisProgress) -> Outcome:
        files = {piece: self._library.resolve(path)
                 for piece, path in request.drum_inputs.pieces().items()}  # fmt: skip
        hits = self.parts.drums.transcribe(files, None, scaled(report, MIXED, FINISHING))
        events = sorted((_event(hit) for hit in hits), key=_kit_order)
        result = DrumHits(engine=self.parts.drum_engine, pieces=list(files),
                          hits=_capped(events))  # fmt: skip
        return _completed(result)

    def _vocal_pitch(self, request: VocalPitchJobRequest, report: AnalysisProgress) -> Outcome:
        audio = self.parts.mixer.mix([self._library.resolve(request.input)], PITCH_SAMPLE_RATE)
        report(_PITCH_LOADED)
        pitch = self.parts.pitch.analyze(audio, request.source,
                                         scaled(report, _PITCH_LOADED, FINISHING))  # fmt: skip
        return _completed(pitch)


class Context:
    """What the service takes from the runtime: the library reader and the clock."""

    def __init__(self, library: LibraryReader, clock: Clock) -> None:
        """Keep ``library`` (path guard and resolution) and ``clock``."""
        self.library = library
        self.clock = clock


def _check(library: LibraryReader, path: str) -> None:
    """The path guard; a library that cannot be read now fails the job when it runs."""
    try:
        library.resolve(path)
    except (InputPathError, InputNotFoundError):
        raise
    except RenderError:
        return


def _kit_order(event: DrumHitEvent) -> tuple[int, int]:
    return event.time_ms, _KIT.index(event.piece)


def _capped(events: list[DrumHitEvent]) -> list[DrumHitEvent]:
    """At most ``MAX_DRUM_HITS``: each piece keeps its share, strongest hits first."""
    if len(events) <= MAX_DRUM_HITS:
        return events
    _LOG.warning("Drum hits capped from %d to %d (a share per piece, strongest first)",
                 len(events), MAX_DRUM_HITS)  # fmt: skip
    by_piece = _by_piece(events)
    shares = _shares({piece: len(hits) for piece, hits in by_piece.items()}, MAX_DRUM_HITS)
    kept = (
        event
        for piece, hits in by_piece.items()
        for event in heapq.nsmallest(shares[piece], hits, key=lambda e: -e.strength)
    )
    return sorted(kept, key=_kit_order)


def _by_piece(events: list[DrumHitEvent]) -> dict[DrumPiece, list[DrumHitEvent]]:
    """The hits of every piece, in their order."""
    by_piece: dict[DrumPiece, list[DrumHitEvent]] = {}
    for event in events:
        by_piece.setdefault(event.piece, []).append(event)
    return by_piece


def _shares(counts: dict[DrumPiece, int], cap: int) -> dict[DrumPiece, int]:
    """``cap`` split in proportion to ``counts`` (largest remainder; ties in kit order)."""
    total = sum(counts.values())
    shares = {piece: count * cap // total for piece, count in counts.items()}
    by_remainder = sorted(counts, key=lambda p: (-(counts[p] * cap % total), _KIT.index(p)))
    for piece in by_remainder[: cap - sum(shares.values())]:
        shares[piece] += 1
    return shares


def _event(hit: Any) -> DrumHitEvent:
    strength = min(max(float(hit.strength), 0.0), 1.0)
    return DrumHitEvent(time_ms=max(round(hit.time * 1000), 0), piece=hit.piece, strength=strength)


def _completed(result: Any, **changes: Any) -> Outcome:
    return {"status": _S.COMPLETED, "progress": 1.0, "result": result, **changes}


class _Reporter:
    """Progress of one job: stores progress and stage, stops a deleted job."""

    def __init__(
        self, service: InstrumentAnalysisService, job_id: str, first: InstrumentAnalysisStage
    ) -> None:
        self._service = service
        self._job_id = job_id
        self._first = first
        self._last = 0.0

    def __call__(self, fraction: float, /) -> None:
        self._last = max(self._last, min(max(fraction, 0.0), 1.0))
        stage = stage_of(self._last, self._first)
        if not self._service.update(self._job_id, progress=self._last, stage=stage):
            raise AnalysisCancelledError("The analysis job was deleted")


def stage_of(progress: float, first: InstrumentAnalysisStage) -> InstrumentAnalysisStage:
    """The stage of a running job at ``progress`` (``first`` up to the end of the mixdown)."""
    if progress <= MIXED:
        return first
    return "analysing" if progress < FINISHING else "finishing"
