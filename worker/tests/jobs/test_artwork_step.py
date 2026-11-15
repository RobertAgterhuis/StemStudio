"""Tests of the "Saving cover" step of the standard job (005 T024, FR-001, FR-002, SC-006)."""

import threading
from pathlib import Path

import pytest

from stemstudio_worker.jobs.artwork import ArtworkCapture, CoverStep, stage_upload_artwork
from stemstudio_worker.jobs.models import StoredJob
from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.progress import JobTracker
from stemstudio_worker.jobs.runner import JobContext, JobRunner
from stemstudio_worker.jobs.service import JobService
from stemstudio_worker.ports import ArtworkCandidate, ArtworkFetchError, SourceInfo
from stemstudio_worker.schemas import Job, JobState, SourceType

from .artwork_fakes import SPOTIFY_COVER, YOUTUBE_THUMB, FakeNormalizer, images_for
from .fakes import FakeClock, Fakes, RecordingStore

S = JobState
KEY = "youtube:dQw4w9WgXcQ"
WATCH = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
YOUTUBE = SourceInfo(SourceType.YOUTUBE, KEY, "YouTube video dQw4w9WgXcQ", None, WATCH)
SPOTIFY = SourceInfo(
    SourceType.SPOTIFY, "spotify:4cOdK2wGLETKBW3PvgPWqT", "Hero of the Day", "Metallica", WATCH,
    album="Load", isrc="USEE19600011", artwork=ArtworkCandidate("spotify", url=SPOTIFY_COVER),
    lookup_title="Hero of the Day", lookup_artist="Metallica",
)  # fmt: skip
THUMB = ArtworkCandidate("youtube", url=YOUTUBE_THUMB)


class Env:
    """Runner + service over a real store, the pipeline fakes and a fake normaliser."""

    def __init__(self, root: Path, timeout: float = 5.0, capture: bool = True) -> None:
        self.paths = DataPaths(root)
        self.store = RecordingStore(self.paths.jobs)
        self.fakes = Fakes.create()
        self.normalizer = FakeNormalizer()
        clock = FakeClock()
        step = ArtworkCapture(self.normalizer, timeout) if capture else None
        context = JobContext(self.store, self.paths, clock)
        self.runner = JobRunner(context, self.fakes.adapters(), step)
        self.service = JobService(self.store, lambda _job_id: None, clock)

    def run(self, source: SourceInfo, job_id: str | None = None, upload: bool = False) -> Job:
        job, created = self.service.create(source, job_id, "input.mp3" if upload else None)
        assert created
        self.store.history.clear()
        self.runner.process(job.job_id)
        return self.record(job.job_id).job

    def record(self, job_id: str) -> StoredJob:
        record = self.store.get(job_id)
        assert record is not None
        return record

    def stages(self) -> list[str]:
        stages: list[str] = []
        for _, stage, _ in self.store.history:
            if not stages or stages[-1] != stage:
                stages.append(stage)
        return stages


@pytest.fixture
def env(tmp_path: Path) -> Env:
    return Env(tmp_path)


def _youtube_with_thumbnail(env: Env) -> Job:
    env.fakes.ingest.fetched_extra = {"artwork": THUMB}
    return env.run(YOUTUBE)


# ------------------------------------------------------------------ the step


def test_saving_cover_runs_after_the_download_and_before_conversion(env: Env) -> None:
    _youtube_with_thumbnail(env)

    assert env.stages()[:3] == ["Downloading audio", "Saving cover", "Preparing audio"]


def test_saving_cover_does_not_change_the_progress(env: Env) -> None:
    _youtube_with_thumbnail(env)

    saving = {(s, p) for s, stage, p in env.store.history if stage == "Saving cover"}
    downloaded = max(p for _, stage, p in env.store.history if stage == "Downloading audio")
    assert saving == {(S.DOWNLOADING, downloaded)}


def test_the_job_artwork_is_set_and_its_files_are_written(env: Env) -> None:
    job = _youtube_with_thumbnail(env)

    assert job.status is S.COMPLETED
    assert job.artwork == images_for("youtube").info("youtube", YOUTUBE_THUMB)
    folder = env.paths.job_artwork(job.job_id)
    assert (folder / "cover.jpg").read_bytes() == b"cover:youtube"
    assert (folder / "thumb.jpg").read_bytes() == b"thumb:youtube"
    assert env.normalizer.seen == [THUMB]


def test_the_spotify_cover_beats_the_youtube_thumbnail(env: Env) -> None:
    env.fakes.ingest.fetched_extra = {"artwork": THUMB}

    job = env.run(SPOTIFY)

    assert job.artwork is not None
    assert (job.artwork.source, job.artwork.origin_url) == ("spotify", SPOTIFY_COVER)
    assert [c.source for c in env.normalizer.seen] == ["spotify"]


def test_the_next_candidate_is_used_when_the_best_one_fails(env: Env) -> None:
    env.fakes.ingest.fetched_extra = {"artwork": THUMB}
    env.normalizer.fail["spotify"] = ArtworkFetchError("host not allowed")

    job = env.run(SPOTIFY)

    assert job.artwork is not None
    assert job.artwork.source == "youtube"


@pytest.mark.parametrize("error", [ArtworkFetchError("offline"), RuntimeError("decoder crash")])
def test_any_artwork_error_is_logged_and_the_job_still_completes(
    env: Env, caplog: pytest.LogCaptureFixture, error: Exception
) -> None:
    env.normalizer.fail["youtube"] = error

    job = _youtube_with_thumbnail(env)

    assert (job.status, job.artwork) == (S.COMPLETED, None)
    assert "cover could not be saved" in caplog.text
    assert not env.paths.job_artwork(job.job_id).exists()


def test_a_normaliser_slower_than_the_budget_is_abandoned(
    tmp_path: Path, caplog: pytest.LogCaptureFixture
) -> None:
    env = Env(tmp_path, timeout=0.05)
    env.normalizer.hang = threading.Event()
    try:
        job = _youtube_with_thumbnail(env)
    finally:
        env.normalizer.hang.set()

    assert (job.status, job.artwork) == (S.COMPLETED, None)
    assert "cover could not be saved" in caplog.text


def test_without_a_candidate_there_is_no_cover_step(env: Env) -> None:
    job = env.run(YOUTUBE)

    assert job.artwork is None
    assert "Saving cover" not in env.stages()
    assert env.normalizer.seen == []


def test_without_a_normaliser_candidates_are_ignored(tmp_path: Path) -> None:
    env = Env(tmp_path, capture=False)

    job = _youtube_with_thumbnail(env)

    assert (job.status, job.artwork) == (S.COMPLETED, None)
    assert "Saving cover" not in env.stages()


def test_an_embedded_upload_cover_is_staged_and_normalised(env: Env) -> None:
    job_id = "d" * 32
    upload_dir = env.paths.uploads(job_id)
    upload_dir.mkdir(parents=True)
    (upload_dir / "input.mp3").write_bytes(b"ID3")
    cover = ArtworkCandidate("embedded", data=b"\x89PNG embedded")
    source = SourceInfo(SourceType.UPLOAD, "upload:" + "c" * 64, "Song", None, None, artwork=cover)
    stage_upload_artwork(upload_dir, source)

    job = env.run(source, job_id=job_id, upload=True)

    assert env.normalizer.seen == [cover]
    assert job.artwork == images_for("embedded").info("embedded")
    assert env.stages()[:2] == ["Saving cover", "Preparing audio"]
    assert not upload_dir.exists()


def test_staging_ignores_sources_without_embedded_bytes(tmp_path: Path) -> None:
    stage_upload_artwork(tmp_path / "a", YOUTUBE)
    stage_upload_artwork(tmp_path / "b", SPOTIFY)

    assert list(tmp_path.iterdir()) == []


def test_an_upload_whose_staged_cover_vanished_has_no_artwork(env: Env) -> None:
    job_id = "d" * 32
    env.paths.uploads(job_id).mkdir(parents=True)
    (env.paths.uploads(job_id) / "input.mp3").write_bytes(b"ID3")
    cover = ArtworkCandidate("embedded", data=b"\x89PNG")
    source = SourceInfo(SourceType.UPLOAD, "upload:" + "c" * 64, "Song", None, None, artwork=cover)

    job = env.run(source, job_id=job_id, upload=True)

    assert (job.status, job.artwork) == (S.COMPLETED, None)


def test_the_pending_spotify_cover_is_persisted_with_the_queued_job(env: Env) -> None:
    job, _ = env.service.create(SPOTIFY)

    reloaded = RecordingStore(env.paths.jobs)
    reloaded.load_all()
    record = reloaded.get(job.job_id)
    assert record is not None
    assert record.artwork_candidate is not None
    assert (record.artwork_candidate.source, record.artwork_candidate.url) == (
        "spotify",
        SPOTIFY_COVER,
    )


# ------------------------------------------------------------------ metadata (005 fields)


def test_duration_is_set_after_normalisation(env: Env) -> None:
    job = env.run(YOUTUBE)

    assert job.metadata is not None
    assert job.metadata.duration_seconds == 12.5
    result = env.record(job.job_id).result
    assert result is not None
    assert result.metadata == job.metadata


def test_spotify_metadata_carries_album_isrc_and_lookup_names(env: Env) -> None:
    job, _ = env.service.create(SPOTIFY)

    assert job.metadata is not None
    assert (job.metadata.album, job.metadata.isrc) == ("Load", "USEE19600011")
    assert (job.metadata.lookup_title, job.metadata.lookup_artist) == (
        "Hero of the Day",
        "Metallica",
    )


def test_youtube_download_fields_fill_album_and_lookup_names(env: Env) -> None:
    env.fakes.ingest.fetched_title = "Rick Astley - Never Gonna Give You Up (Official Video)"
    env.fakes.ingest.fetched_extra = {
        "album": "Whenever You Need Somebody",
        "lookup_title": "Never Gonna Give You Up",
        "lookup_artist": "Rick Astley",
    }

    job = env.run(YOUTUBE)

    assert job.metadata is not None
    assert job.metadata.album == "Whenever You Need Somebody"
    assert (job.metadata.lookup_title, job.metadata.lookup_artist) == (
        "Never Gonna Give You Up",
        "Rick Astley",
    )


# ------------------------------------------------------------------ cached results


def test_a_source_cache_hit_keeps_the_artwork_of_the_first_job(env: Env) -> None:
    first = _youtube_with_thumbnail(env)
    failed = first.model_copy(update={"status": S.FAILED})
    env.store.save(env.record(first.job_id).model_copy(update={"job": failed}))
    env.fakes.ingest.fetched_extra = {}
    env.fakes.calls.names.clear()

    second = env.run(YOUTUBE)

    assert env.fakes.calls.names == []
    assert second.artwork == first.artwork
    folder = env.paths.job_artwork(second.job_id)
    assert (folder / "cover.jpg").read_bytes() == b"cover:youtube"


def test_a_cache_hit_reports_the_cached_duration(env: Env) -> None:
    first = env.run(YOUTUBE)
    failed = first.model_copy(update={"status": S.FAILED})
    env.store.save(env.record(first.job_id).model_copy(update={"job": failed}))

    second = env.run(YOUTUBE)

    assert second.metadata is not None
    assert second.metadata.duration_seconds == 12.5


def test_an_audio_cache_hit_without_own_picture_gets_the_cached_one(env: Env) -> None:
    _youtube_with_thumbnail(env)
    env.fakes.ingest.fetched_extra = {}
    other = SourceInfo(SourceType.YOUTUBE, "youtube:aaaaaaaaaaa", "Other", None, WATCH)

    job = env.run(other)

    assert job.artwork is not None
    assert job.artwork.source == "youtube"


def test_an_audio_cache_hit_keeps_its_own_better_picture(env: Env) -> None:
    _youtube_with_thumbnail(env)

    job = env.run(SPOTIFY)

    assert job.artwork is not None
    assert job.artwork.source == "spotify"
    cached = env.paths.cached_artwork("e" * 64)
    assert (cached / "cover.jpg").read_bytes() == b"cover:spotify"


def test_a_recorded_picture_whose_files_vanished_is_not_cached(env: Env) -> None:
    job = _youtube_with_thumbnail(env)
    for path in env.paths.job_artwork(job.job_id).iterdir():
        path.unlink()
    tracker = JobTracker(env.store, job.job_id, FakeClock())

    CoverStep(env.paths, None).sync(tracker, "f" * 64)

    assert not env.paths.cached_artwork("f" * 64).exists()
