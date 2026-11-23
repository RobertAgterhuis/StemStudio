"""Unit tests of the instrument analysis service (016 T062): stages, cancel, stand-in parts."""

from pathlib import Path

import pytest

from stemstudio_worker.jobs.instrument_analysis_chords import MIXED
from stemstudio_worker.jobs.instrument_analysis_ports import (
    UNAVAILABLE_TEXT,
    InstrumentAnalysisParts,
    unavailable_parts,
)
from stemstudio_worker.jobs.instrument_analysis_service import (
    FINISHING,
    Context,
    InstrumentAnalysisService,
    stage_of,
)
from stemstudio_worker.jobs.instrument_analysis_store import InstrumentAnalysisStore
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.render_service import NoLibrary
from stemstudio_worker.ports import AnalysisCancelledError, AnalysisError
from stemstudio_worker.schemas import (
    ChordsJobRequest,
    DrumHitsJobRequest,
    DrumInputs,
    InstrumentAnalysisModels,
)

from .conftest import T0

TRACK = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"


class _Chords:
    def __init__(self, models: InstrumentAnalysisModels, default: str = "btc") -> None:
        self._models = models
        self.default_engine = default

    def models(self) -> InstrumentAnalysisModels:
        return self._models


def _service(
    tmp_path: Path, parts: InstrumentAnalysisParts | None = None
) -> InstrumentAnalysisService:
    store = InstrumentAnalysisStore.of(DataPaths(tmp_path))
    return InstrumentAnalysisService(
        store, parts or unavailable_parts(), Context(NoLibrary(), lambda: T0)
    )


def _chords(vocabulary: str = "simple", engine: str | None = None) -> ChordsJobRequest:
    return ChordsJobRequest(kind="chords", inputs=[f"{TRACK}/stems/guitar.flac"],
                            vocabulary=vocabulary, engine=engine)  # type: ignore[arg-type]  # fmt: skip


def _parts(chords: _Chords) -> InstrumentAnalysisParts:
    base = unavailable_parts()
    return InstrumentAnalysisParts(chords=chords, drums=base.drums, drum_engine="drum-onsets",  # type: ignore[arg-type]
                                   pitch=base.pitch, mixer=base.mixer)  # fmt: skip


@pytest.mark.parametrize(
    ("progress", "stage"),
    [
        (0.0, "loading-model"),
        (MIXED, "loading-model"),
        (0.06, "analysing"),
        (FINISHING, "finishing"),
    ],
)
def test_the_stage_follows_the_progress(progress: float, stage: str) -> None:
    assert stage_of(progress, "loading-model") == stage


def test_a_btc_job_without_cached_weights_starts_with_loading_model(tmp_path: Path) -> None:
    service = _service(tmp_path, _parts(_Chords(InstrumentAnalysisModels(btc_majmin="ready"))))

    assert service._first_stage(_chords()) == "preparing"
    assert service._first_stage(_chords("full")) == "loading-model"
    assert service._first_stage(_chords(engine="chroma-hmm")) == "preparing"
    drums = DrumHitsJobRequest(
        kind="drum-hits", drum_inputs=DrumInputs(kick=f"{TRACK}/detail/drums/kick.flac")
    )
    assert service._first_stage(drums) == "preparing"


def test_a_configured_chroma_hmm_engine_starts_with_preparing(tmp_path: Path) -> None:
    service = _service(tmp_path, _parts(_Chords(InstrumentAnalysisModels(), "chroma-hmm")))

    assert service._first_stage(_chords()) == "preparing"


def test_processing_an_unknown_job_does_nothing(tmp_path: Path) -> None:
    service = _service(tmp_path)

    service.process("a" * 32)

    assert service.get("a" * 32) is None


def test_a_progress_report_of_a_deleted_job_cancels_it(tmp_path: Path) -> None:
    from stemstudio_worker.jobs.instrument_analysis_service import _Reporter  # noqa: PLC0415

    report = _Reporter(_service(tmp_path), "b" * 32, "preparing")

    with pytest.raises(AnalysisCancelledError):
        report(0.5)


def test_the_stand_in_parts_fail_every_analysis_readably() -> None:
    parts = unavailable_parts()

    assert parts.pitch_engine == "pyin" and parts.chords.default_engine == "btc"
    assert parts.chords.models() == InstrumentAnalysisModels()
    for call in (lambda: parts.chords.analyze(None, None, None),  # type: ignore[arg-type]
                 lambda: parts.drums.transcribe({}, None, lambda _f: None),
                 lambda: parts.mixer.mix([], 16_000)):  # fmt: skip
        with pytest.raises(AnalysisError, match=UNAVAILABLE_TEXT):
            call()


def test_a_worker_without_parts_fails_its_jobs_with_the_stand_in_text(tmp_path: Path) -> None:
    service = _service(tmp_path)
    job = service.create(_chords())

    service.process(job.analysis_job_id)

    done = service.get(job.analysis_job_id)
    assert done is not None and done.status.value == "failed"
    assert done.error is not None
