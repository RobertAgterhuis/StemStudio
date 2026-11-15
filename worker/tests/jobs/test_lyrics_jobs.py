"""Tests of the lyrics jobs: lookups on the network lane, alignments as scheduler units (006 T017).

Lookups run on the 005 network lane; alignments are units of the priority scheduler (standard >
align > detail parent step, see ``test_scheduler.py``) through the 003 device runner. Jobs are
persisted in ``lyrics-jobs/``, re-run after a restart, cancelled, deleted and swept (FR-014).
"""

import functools
import json
from collections.abc import Iterator
from datetime import datetime, timedelta
from pathlib import Path

import pytest

from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.jobs.detail_service import UnknownJobError
from stemstudio_worker.jobs.lanes import LaneScheduler
from stemstudio_worker.jobs.lyrics_lane import (
    LyricsConflictError,
    LyricsContext,
    LyricsParts,
    LyricsQueues,
    LyricsRunner,
    LyricsService,
)
from stemstudio_worker.jobs.lyrics_store import LyricsJobStore, StoredLyricsJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.scheduler import PriorityScheduler
from stemstudio_worker.jobs.store import JobStore
from stemstudio_worker.ports import (
    WHISPER_GPU_NEED,
    AlignmentError,
    LyricsQuery,
    LyricsUnavailableError,
    estimate_alignment_seconds,
)
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    LYRICS_STAGE_DOWNLOADING,
    LYRICS_UNREACHABLE_TEXT,
    VOCAL_STEM_MISSING_TEXT,
    VOCAL_STEM_SILENT_TEXT,
    JobState,
    LyricsJob,
    LyricsJobState,
    LyricsResult,
)

from .conftest import T0, build_job
from .detail_env import FOUR, SHA, STD, completed_standard, write_tone
from .lyrics_fakes import (
    ALIGNED,
    FOUND,
    LOOKUP,
    TEXT,
    WAIT,
    FakeAligner,
    FakeLyricsLookup,
    FakeVocals,
    align_request,
    timed,
)

S = LyricsJobState


class Clock:
    """A settable UTC clock (starts at T0)."""

    def __init__(self) -> None:
        self.now = T0

    def __call__(self) -> datetime:
        return self.now


class Env:
    """A data directory with a completed standard job, fakes, a network lane and a scheduler."""

    def __init__(self, root: Path) -> None:
        self.paths = DataPaths(root)
        self.jobs = JobStore(self.paths.jobs)
        completed_standard(self.paths, self.jobs, FOUR)
        self.clock = Clock()
        self.lookup = FakeLyricsLookup()
        self.aligner = FakeAligner()
        self.vocals = FakeVocals()
        self.devices = FakeDeviceRunner("gpu")
        self.cached = False
        self._threads: list[LaneScheduler | PriorityScheduler] = []

    def start(self) -> LyricsService:
        store = LyricsJobStore(self.paths)
        store.load_all()
        context = LyricsContext(store, self.jobs, self.paths, self.clock)
        parts = LyricsParts(self.lookup, self.aligner, self.vocals, self.devices,
                            model_cached=lambda: self.cached)  # fmt: skip
        runner = LyricsRunner(context, parts)
        self.network = LaneScheduler("network", lambda _job: None)
        self.scheduler = PriorityScheduler(lambda _job: None, lambda _d: False, align=runner.align)
        queues = LyricsQueues(
            lookup=functools.partial(self.network.submit_call, runner.lookup),
            align=self.scheduler.submit_align,
        )
        service = LyricsService.start(runner, queues)
        for thread in (self.network, self.scheduler):
            thread.start()
            self._threads.append(thread)
        return service

    def wait(self) -> None:
        assert self.network.join(WAIT)
        assert self.scheduler.join(WAIT)

    def stop(self) -> None:
        for thread in self._threads:
            thread.stop(WAIT)

    def seed(self, job: LyricsJob, request: object) -> None:
        """Persist a lyrics job as an earlier worker run would have left it."""
        record = StoredLyricsJob.model_validate({"job": job, "request": request})
        LyricsJobStore(self.paths).save(record)

    def lead(self) -> Path:
        """Write the ``vocals.lead`` detail stem of the standard job."""
        path = self.paths.detail_stem(SHA, "vocals", "lead")
        path.parent.mkdir(parents=True, exist_ok=True)
        write_tone(path)
        return path


@pytest.fixture
def env(tmp_path: Path) -> Iterator[Env]:
    environment = Env(tmp_path)
    yield environment
    environment.stop()


def _job(job_id: str, mode: str, status: S, age: timedelta = timedelta()) -> LyricsJob:
    stamp = T0 - age
    return LyricsJob.model_validate(
        {"lyricsJobId": job_id, "mode": mode, "status": status, "stage": "Aligning lyrics",
         "progress": 0.5, "createdAt": stamp, "updatedAt": stamp}
    )  # fmt: skip


# ------------------------------------------------------------------ lookups


def test_a_lookup_completes_on_the_network_lane_with_its_result(env: Env) -> None:
    service = env.start()

    job = service.create(LOOKUP)
    env.wait()

    assert (job.mode, job.status, job.stage, job.progress) == ("lookup", S.QUEUED, "Queued", 0)
    assert (job.device, job.estimated_seconds) == (None, None)
    assert env.lookup.queries == [LyricsQuery("Fake Song", 180.0, "Fake Band", None)]
    done = service.get(job.lyrics_job_id)
    assert (done.status, done.stage, done.progress, done.error) == (S.COMPLETED, "Completed", 1, None)
    assert service.result(job.lyrics_job_id) == FOUND
    assert (env.paths.lyrics_jobs / f"{job.lyrics_job_id}.json").is_file()


def test_a_running_lookup_shows_the_searching_stage(env: Env) -> None:
    release = env.lookup.gate.block()
    service = env.start()

    job = service.create(LOOKUP)
    assert env.lookup.gate.entered.wait(WAIT)

    running = service.get(job.lyrics_job_id)
    assert (running.status, running.stage) == (S.RUNNING, "Searching lyrics")
    release.set()
    env.wait()


def test_an_unreachable_lrclib_fails_the_lookup_with_the_fixed_text(env: Env) -> None:
    env.lookup.error = LyricsUnavailableError()
    service = env.start()

    job = service.create(LOOKUP)
    env.wait()

    failed = service.get(job.lyrics_job_id)
    assert (failed.status, failed.stage, failed.error) == (S.FAILED, "Failed", LYRICS_UNREACHABLE_TEXT)
    with pytest.raises(LyricsConflictError) as raised:
        service.result(job.lyrics_job_id)
    assert raised.value.kind == "lyrics-not-completed"


def test_an_unexpected_lookup_error_is_logged_without_the_lyrics_and_fails_the_job(
    env: Env, caplog: pytest.LogCaptureFixture
) -> None:
    env.lookup.error = RuntimeError("boom")
    service = env.start()

    job = service.create(LOOKUP)
    env.wait()

    assert service.get(job.lyrics_job_id).error == LYRICS_UNREACHABLE_TEXT
    assert "failed unexpectedly" in caplog.text


def test_a_lookup_cannot_be_cancelled(env: Env) -> None:
    service = env.start()
    job = service.create(LOOKUP)
    env.wait()

    with pytest.raises(LyricsConflictError) as raised:
        service.cancel(job.lyrics_job_id)

    assert raised.value.kind == "lyrics-job-not-cancellable"
    assert str(raised.value) == "Lyrics job is in state completed."


# ------------------------------------------------------------------ alignments


def test_an_alignment_runs_on_the_vocal_stem_through_the_device_runner(env: Env) -> None:
    service = env.start()

    job = service.create(align_request(STD, language="en"))
    env.wait()

    expected = estimate_alignment_seconds(0.5, "gpu", model_cached=False)
    assert (job.mode, job.status, job.estimated_seconds, job.device) == ("align", S.QUEUED, expected, None)
    assert env.vocals.paths == [env.paths.stem_file(SHA, "vocals")]
    request, device = env.aligner.calls[0]
    assert (request.text, request.language, request.refine, device) == (TEXT, "en", None, "cuda")
    assert request.audio.dtype.name == "float32"
    done = service.get(job.lyrics_job_id)
    assert (done.status, done.progress, done.stage) == (S.COMPLETED, 1, "Completed")
    assert done.device is not None
    assert (done.device.kind, done.device.name, done.device.cpu_fallback) == ("gpu", "Fake GPU", False)
    assert service.result(job.lyrics_job_id) == LyricsResult(outcome="found", lyrics=ALIGNED)


def test_the_lead_vocal_detail_stem_is_preferred(env: Env) -> None:
    lead = env.lead()
    service = env.start()

    service.create(align_request(STD))
    env.wait()

    assert env.vocals.paths == [lead]


def test_the_estimate_drops_the_download_when_the_model_is_cached(env: Env) -> None:
    env.cached = True
    env.devices = FakeDeviceRunner("cpu")
    service = env.start()

    job = service.create(align_request(STD))
    env.wait()

    assert job.estimated_seconds == estimate_alignment_seconds(0.5, "cpu", model_cached=True)


def test_a_cpu_fallback_is_recorded_on_the_job(env: Env) -> None:
    env.devices = FakeDeviceRunner("gpu-oom")
    service = env.start()

    job = service.create(align_request(STD))
    env.wait()

    device = service.get(job.lyrics_job_id).device
    assert device is not None
    assert (device.kind, device.cpu_fallback) == ("cpu", True)
    assert device.fallback_reason is not None
    assert WHISPER_GPU_NEED.label in device.fallback_reason
    assert env.aligner.calls[0][1] == "cpu"


def test_a_running_alignment_reports_the_aligner_stage_and_progress(env: Env) -> None:
    env.aligner.steps = ((0.1, LYRICS_STAGE_DOWNLOADING), (0.05, "Aligning lyrics"))
    release = env.aligner.gate.block()
    service = env.start()

    job = service.create(align_request(STD))
    assert env.aligner.gate.entered.wait(WAIT)
    running = service.get(job.lyrics_job_id)
    assert running.status is S.RUNNING
    assert running.device is not None
    assert running.device.kind == "gpu"
    release.set()
    env.wait()

    assert service.get(job.lyrics_job_id).status is S.COMPLETED


def test_progress_never_decreases(env: Env) -> None:
    env.aligner.steps = ((0.6, LYRICS_STAGE_DOWNLOADING), (0.2, "Aligning lyrics"))
    env.aligner.error = AlignmentError(ALIGNMENT_FAILED_TEXT)
    service = env.start()

    job = service.create(align_request(STD))
    env.wait()

    failed = service.get(job.lyrics_job_id)
    assert (failed.progress, failed.stage, failed.error) == (0.6, "Failed", ALIGNMENT_FAILED_TEXT)


def test_a_refined_alignment_keeps_the_source_of_the_line_document(env: Env) -> None:
    refine = timed("interpolated", "lrclib", "line").model_copy(update={"source_id": "77"})
    service = env.start()

    job = service.create(align_request(STD, refine=refine.model_dump(by_alias=True)))
    env.wait()

    assert env.aligner.calls[0][0].refine == refine
    result = service.result(job.lyrics_job_id)
    assert result.lyrics is not None
    assert (result.lyrics.source, result.lyrics.source_id) == ("lrclib", "77")


def test_a_silent_vocal_stem_fails_before_the_model_step(env: Env) -> None:
    env.vocals.error = AlignmentError(VOCAL_STEM_SILENT_TEXT)
    service = env.start()

    job = service.create(align_request(STD))
    env.wait()

    failed = service.get(job.lyrics_job_id)
    assert (failed.status, failed.error, failed.device) == (S.FAILED, VOCAL_STEM_SILENT_TEXT, None)
    assert env.aligner.calls == []


def test_a_vocal_stem_that_left_the_cache_fails_the_alignment(env: Env) -> None:
    release = env.aligner.gate.block()
    service = env.start()
    first = service.create(align_request(STD))
    assert env.aligner.gate.entered.wait(WAIT)
    second = service.create(align_request(STD))
    env.paths.stem_file(SHA, "vocals").unlink()
    release.set()
    env.wait()

    assert service.get(first.lyrics_job_id).status is S.COMPLETED
    assert service.get(second.lyrics_job_id).error == VOCAL_STEM_MISSING_TEXT


def test_an_unexpected_alignment_error_fails_with_the_fixed_text(
    env: Env, caplog: pytest.LogCaptureFixture
) -> None:
    env.aligner.error = ValueError(TEXT)
    service = env.start()

    job = service.create(align_request(STD))
    env.wait()

    assert service.get(job.lyrics_job_id).error == ALIGNMENT_FAILED_TEXT
    assert "failed unexpectedly" in caplog.text
    assert "Fake line one" not in caplog.text


def test_an_alignment_needs_a_known_completed_standard_job_with_vocals(env: Env) -> None:
    env.jobs.save(env.jobs.get(STD).model_copy(  # type: ignore[union-attr]
        update={"job": build_job(job_id="c" * 32, status=JobState.SEPARATING)}
    ))  # fmt: skip
    service = env.start()

    with pytest.raises(UnknownJobError):
        service.create(align_request("d" * 32))
    with pytest.raises(LyricsConflictError) as running:
        service.create(align_request("c" * 32))
    env.paths.stem_file(SHA, "vocals").unlink()
    with pytest.raises(LyricsConflictError) as missing:
        service.create(align_request(STD))

    assert (running.value.kind, str(running.value)) == (
        "standard-job-not-completed", f"Job {'c' * 32} is in state separating."
    )
    assert (missing.value.kind, str(missing.value)) == ("vocal-stem-missing", VOCAL_STEM_MISSING_TEXT)


# ------------------------------------------------------------------ cancel and delete


def test_cancelling_a_queued_alignment_keeps_it_from_running(env: Env) -> None:
    release = env.aligner.gate.block()
    service = env.start()
    first = service.create(align_request(STD))
    assert env.aligner.gate.entered.wait(WAIT)
    second = service.create(align_request(STD))

    cancelled = service.cancel(second.lyrics_job_id)
    release.set()
    env.wait()

    assert (cancelled.status, cancelled.stage) == (S.CANCELLED, "Cancelled")
    assert len(env.aligner.calls) == 1
    assert service.get(first.lyrics_job_id).status is S.COMPLETED
    assert service.get(second.lyrics_job_id).status is S.CANCELLED


def test_cancelling_a_running_alignment_stops_it_at_the_next_progress_report(env: Env) -> None:
    release = env.aligner.gate.block()
    service = env.start()
    job = service.create(align_request(STD))
    assert env.aligner.gate.entered.wait(WAIT)

    service.cancel(job.lyrics_job_id)
    release.set()
    env.wait()

    final = service.get(job.lyrics_job_id)
    assert (final.status, final.stage, final.error) == (S.CANCELLED, "Cancelled", None)
    with pytest.raises(LyricsConflictError):
        service.result(job.lyrics_job_id)
    with pytest.raises(LyricsConflictError):
        service.cancel(job.lyrics_job_id)


def test_an_aligner_that_ignores_the_cancel_does_not_complete_the_job(env: Env) -> None:
    env.aligner.steps = ()
    release = env.aligner.gate.block()
    service = env.start()
    job = service.create(align_request(STD))
    assert env.aligner.gate.entered.wait(WAIT)

    service.cancel(job.lyrics_job_id)
    release.set()
    env.wait()

    assert service.get(job.lyrics_job_id).status is S.CANCELLED
    assert not env.paths.lyrics_result(job.lyrics_job_id).exists()


def test_delete_removes_the_job_and_its_result(env: Env) -> None:
    service = env.start()
    job = service.create(LOOKUP)
    env.wait()

    service.delete(job.lyrics_job_id)

    with pytest.raises(UnknownJobError):
        service.get(job.lyrics_job_id)
    with pytest.raises(UnknownJobError):
        service.delete(job.lyrics_job_id)
    with pytest.raises(UnknownJobError):
        service.result(job.lyrics_job_id)
    assert not env.paths.lyrics_job_dir(job.lyrics_job_id).exists()
    assert not (env.paths.lyrics_jobs / f"{job.lyrics_job_id}.json").exists()


def test_deleting_a_running_alignment_stops_it_and_leaves_nothing(env: Env) -> None:
    release = env.aligner.gate.block()
    service = env.start()
    job = service.create(align_request(STD))
    assert env.aligner.gate.entered.wait(WAIT)

    service.delete(job.lyrics_job_id)
    release.set()
    env.wait()

    assert list(env.paths.lyrics_jobs.iterdir()) == []


def test_unknown_jobs_are_reported(env: Env) -> None:
    service = env.start()

    for action in (service.get, service.cancel, service.result, service.delete):
        with pytest.raises(UnknownJobError, match="does not exist"):
            action("e" * 32)


# ------------------------------------------------------------------ restart and sweep


def test_a_restart_runs_unfinished_jobs_again_from_the_start(env: Env) -> None:
    env.seed(_job("1" * 32, "lookup", S.RUNNING), LOOKUP)
    env.seed(_job("2" * 32, "align", S.QUEUED), align_request(STD))
    env.seed(_job("3" * 32, "align", S.FAILED), align_request(STD))

    service = env.start()
    env.wait()

    assert [service.get(i * 32).status for i in "123"] == [S.COMPLETED, S.COMPLETED, S.FAILED]
    assert len(env.lookup.queries) == len(env.aligner.calls) == 1


def test_a_restarted_job_starts_queued_with_no_progress(env: Env) -> None:
    env.seed(_job("2" * 32, "align", S.RUNNING), align_request(STD))
    release = env.aligner.gate.block()

    service = env.start()
    assert env.aligner.gate.entered.wait(WAIT)
    raw = json.loads((env.paths.lyrics_jobs / f"{'2' * 32}.json").read_text(encoding="utf-8"))
    release.set()
    env.wait()

    assert (raw["job"]["status"], raw["job"]["progress"], raw["job"]["stage"]) == (
        "running", 0, "Aligning lyrics"
    )
    assert service.get("2" * 32).status is S.COMPLETED


def test_the_sweep_removes_terminal_jobs_older_than_24_hours(env: Env) -> None:
    env.seed(_job("1" * 32, "lookup", S.COMPLETED, timedelta(hours=25)), LOOKUP)
    env.seed(_job("2" * 32, "align", S.CANCELLED, timedelta(hours=23)), align_request(STD))
    service = env.start()
    env.wait()
    service.create(LOOKUP)
    env.wait()

    assert service.sweep() == ["1" * 32]

    with pytest.raises(UnknownJobError):
        service.get("1" * 32)
    assert service.get("2" * 32).status is S.CANCELLED


def test_an_unreadable_result_file_is_not_served(env: Env) -> None:
    service = env.start()
    job = service.create(LOOKUP)
    env.wait()
    env.paths.lyrics_result(job.lyrics_job_id).write_text("{", encoding="utf-8")

    with pytest.raises(UnknownJobError, match="no longer exists"):
        service.result(job.lyrics_job_id)
