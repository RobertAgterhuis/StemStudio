"""Routes of the ``instrument-analysis`` lane (worker 1.9.0, 016 T061, T084, T093).

``POST /v1/instrument-analysis-jobs`` (202 + Location; 400 path guard, 404 missing input, 422
schema), ``GET`` (404 unknown) and an idempotent ``DELETE`` (204; 400 malformed id). Jobs run on
their own lane, never behind the transcribe, render or practice lanes; stages and progress are
reported while running; results are inline (``ChordChart``, ``DrumHits``, ``VocalPitch``) and
validate against the contract. ``/health`` reports the configured engines and the lane.
"""

import logging
from collections.abc import Callable, Iterator
from typing import Any

import pytest

from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import (
    CHORD_SAMPLE_RATE,
    PITCH_SAMPLE_RATE,
    AnalysisError,
    DeviceUse,
    DrumHit,
    NoMelodyError,
    TranscriptionError,
)
from stemstudio_worker.schemas import MAX_DRUM_HITS, NO_MELODY_TEXT

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .instrument_fakes import (
    LEAD,
    PIECES,
    WAIT,
    Fakes,
    chords_body,
    drum,
    drums_body,
    pitch_body,
    stem,
    write_library,
)

JOBS = "/v1/instrument-analysis-jobs"
ONE = JOBS + "/{analysisJobId}"
GPU = DeviceUse(kind="gpu", torch_device="cuda", name="Fake GPU")


@pytest.fixture
def fakes() -> Fakes:
    return Fakes()


@pytest.fixture
def started(make_api: ApiFactory, fakes: Fakes) -> Iterator[Api]:
    api = make_api(overrides=AppOverrides(instrument_analysis=fakes.parts()))
    write_library(api.settings.render.library_dir)  # type: ignore[arg-type]
    yield api
    for gate in (fakes.chords, fakes.drums, fakes.pitch):
        gate.gate.set()


def _service(api: Api) -> Any:
    return api.client.app.state.runtime.lanes.instrument_analysis  # type: ignore[attr-defined]


def _done(api: Api, job_id: str) -> dict[str, Any]:
    assert _service(api).join(WAIT)
    response = api.client.get(f"{JOBS}/{job_id}")
    assert_conforms(response, ONE)
    body: dict[str, Any] = response.json()
    return body


def _post(api: Api, body: dict[str, Any]) -> str:
    response = api.client.post(JOBS, json=body)
    assert response.status_code == 202, response.text
    assert_conforms(response, JOBS, "post")
    job_id: str = response.json()["analysisJobId"]
    assert response.headers["location"] == f"{JOBS}/{job_id}"
    return job_id


# ------------------------------------------------------------------ chords


def test_a_chords_job_returns_the_chart_with_key_and_regions(started: Api, fakes: Fakes) -> None:
    job_id = _post(started, chords_body())

    job = _done(started, job_id)

    assert (job["status"], job["kind"], job["stage"], job["progress"]) == ("completed", "chords",
                                                                           None, 1)  # fmt: skip
    result = job["result"]
    assert result["kind"] == "chords"
    assert result["key"] == {"tonic": 0, "mode": "major", "confidence": 0.81, "alternative": None}
    assert [r["tonic"] for r in result["keyRegions"]] == [0, 2]
    assert [s["label"] for s in result["segments"]] == ["N", "C:maj", "G:maj"]
    assert job["device"] is None and job["error"] is None


def test_the_chords_job_gets_the_resolved_harmonic_and_bass_files(
    started: Api, fakes: Fakes
) -> None:
    _done(started, _post(started, chords_body()))

    request, inputs = fakes.chords.calls[0]
    root = started.settings.render.library_dir
    assert root is not None
    assert inputs.harmonic == tuple((root / stem(n)).resolve() for n in ("bass", "guitar", "piano",
                                                                          "other"))  # fmt: skip
    assert inputs.bass == (root / stem("bass")).resolve()
    assert request.vocabulary == "simple" and request.grid is not None


def test_a_chords_job_without_bass_input_has_no_bass_file(started: Api, fakes: Fakes) -> None:
    _done(started, _post(started, chords_body(bassInput=None, inputs=[stem("guitar")])))

    assert fakes.chords.calls[0][1].bass is None


def test_the_btc_device_is_recorded_on_the_job(make_api: ApiFactory) -> None:
    gpu = Fakes(device=GPU)
    api = make_api(overrides=AppOverrides(instrument_analysis=gpu.parts()))
    write_library(api.settings.render.library_dir)  # type: ignore[arg-type]

    job = _done(api, _post(api, chords_body()))

    assert job["device"] == {"kind": "gpu", "name": "Fake GPU", "cpuFallback": False,
                             "fallbackReason": None}  # fmt: skip


def test_a_running_job_reports_stage_and_progress(started: Api, fakes: Fakes) -> None:
    fakes.chords.gate.clear()
    job_id = _post(started, chords_body())
    assert fakes.chords.entered.wait(WAIT)

    response = started.client.get(f"{JOBS}/{job_id}")

    assert_conforms(response, ONE)
    job = response.json()
    assert (job["status"], job["stage"], job["progress"]) == ("running", "analysing", 0.5)
    fakes.chords.gate.set()
    assert _done(started, job_id)["status"] == "completed"


def test_the_lane_is_not_blocked_by_the_transcribe_practice_or_render_lanes(
    started: Api,
) -> None:
    runtime = started.client.app.state.runtime  # type: ignore[attr-defined]
    hold = Fakes().chords
    hold.gate.clear()
    busy = [runtime.lanes.transcription._lane, runtime.lanes.render._lane,
            runtime.lanes.practice.lane.scheduler]  # fmt: skip
    for lane in busy:
        lane.submit_call(lambda _job: hold.hold(lambda _f: None), "x")

    job = _done(started, _post(started, chords_body()))

    assert job["status"] == "completed"
    assert all(lane.health().running for lane in busy)
    hold.gate.set()


def test_jobs_on_the_lane_run_one_at_a_time_in_order(started: Api, fakes: Fakes) -> None:
    fakes.chords.gate.clear()
    first = _post(started, chords_body())
    assert fakes.chords.entered.wait(WAIT)
    second = _post(started, pitch_body())

    health = started.client.get("/health").json()["lanes"]["instrumentAnalysis"]
    assert health == {"queued": 1, "running": True}
    assert started.client.get(f"{JOBS}/{second}").json()["status"] == "queued"
    fakes.chords.gate.set()
    assert _done(started, first)["status"] == "completed"
    assert _done(started, second)["status"] == "completed"


def test_an_analysis_error_fails_the_job_with_its_text(started: Api, fakes: Fakes) -> None:
    fakes.chords.error = AnalysisError("The chord engine fake is not available on this worker.")

    job = _done(started, _post(started, chords_body()))

    assert job["status"] == "failed"
    assert job["error"] == "The chord engine fake is not available on this worker."
    assert job["stage"] is None and job["result"] is None


def test_an_unexpected_error_fails_the_job_with_the_generic_text(
    started: Api, fakes: Fakes
) -> None:
    fakes.chords.error = RuntimeError("boom")

    job = _done(started, _post(started, chords_body()))

    assert job["status"] == "failed" and "boom" not in job["error"]


# ------------------------------------------------------------------ drum hits


def test_a_drum_hits_job_returns_the_hits_in_time_order(started: Api, fakes: Fakes) -> None:
    job = _done(started, _post(started, drums_body()))

    result = job["result"]
    assert (job["status"], result["kind"], result["engine"]) == ("completed", "drum-hits",
                                                                 "drum-onsets")  # fmt: skip
    assert result["pieces"] == list(PIECES)
    assert [h["timeMs"] for h in result["hits"]] == [0, 250, 500, 750, 1000]
    assert [h["piece"] for h in result["hits"]] == list(PIECES)
    assert result["hits"][0]["strength"] == 1.0
    root = started.settings.render.library_dir
    assert root is not None
    assert fakes.drums.calls[0] == {p: (root / drum(p)).resolve() for p in PIECES}


def test_missing_drum_pieces_are_not_listed(started: Api, fakes: Fakes) -> None:
    job = _done(started, _post(started, drums_body(("kick", "hihat"))))

    assert job["result"]["pieces"] == ["kick", "hihat"]
    assert set(fakes.drums.calls[0]) == {"kick", "hihat"}


def test_hits_at_the_same_millisecond_keep_the_kit_order(started: Api, fakes: Fakes) -> None:
    # The 008 transcriber orders simultaneous hits kick, snare, toms, hihat, cymbals; the job
    # rounds to milliseconds and must keep that order (not the alphabetical one).
    fakes.drums.hits = (DrumHit(0.4996, "cymbals", 0.8),
                        *(DrumHit(0.5004, piece, 0.8) for piece in PIECES))  # fmt: skip

    job = _done(started, _post(started, drums_body()))

    hits = job["result"]["hits"]
    assert [h["timeMs"] for h in hits] == [500] * 6
    assert [h["piece"] for h in hits] == ["kick", "snare", "toms", "hihat", "cymbals", "cymbals"]


def test_hit_times_and_strengths_are_clamped_to_the_contract(started: Api, fakes: Fakes) -> None:
    fakes.drums.hits = (DrumHit(-0.002, "kick", -0.1), DrumHit(0.1, "snare", 0.4))

    job = _done(started, _post(started, drums_body()))

    hits = job["result"]["hits"]
    assert hits[0] == {"timeMs": 0, "piece": "kick", "strength": 0.0}
    assert hits[1] == {"timeMs": 100, "piece": "snare", "strength": 0.4}


def test_more_hits_than_the_contract_allows_keep_the_first_ones(started: Api, fakes: Fakes) -> None:
    fakes.drums.hits = tuple(DrumHit(i / 1000, "hihat", 0.5) for i in range(MAX_DRUM_HITS + 3))

    job = _done(started, _post(started, drums_body(("hihat",))))

    hits = job["result"]["hits"]
    assert len(hits) == MAX_DRUM_HITS
    assert hits[-1]["timeMs"] == MAX_DRUM_HITS - 1


def test_a_capped_result_keeps_a_fair_share_of_every_piece_and_logs_it(
    started: Api, fakes: Fakes, caplog: pytest.LogCaptureFixture
) -> None:
    """50,000 hits -> 40,000: each piece keeps 80 % (its strongest), the whole song stays."""
    kick = (DrumHit(i / 100, "kick", 0.9 if i % 5 == 0 else 0.3) for i in range(30_000))
    hihat = (DrumHit(i * 0.015, "hihat", 0.5) for i in range(20_000))
    fakes.drums.hits = (*kick, *hihat)

    with caplog.at_level(logging.WARNING):
        job = _done(started, _post(started, drums_body(("kick", "hihat"))))

    hits = job["result"]["hits"]
    kicks = _of(hits, "kick")
    assert (len(kicks), len(_of(hits, "hihat"))) == (24_000, 16_000)
    assert [h["strength"] for h in kicks].count(0.9) == 6_000
    assert kicks[-1]["timeMs"] == 299_950
    times = [h["timeMs"] for h in hits]
    assert times == sorted(times)
    assert "Drum hits capped from 50000 to 40000" in caplog.text


def _of(hits: list[dict[str, Any]], piece: str) -> list[dict[str, Any]]:
    return [h for h in hits if h["piece"] == piece]


def test_the_capped_shares_round_by_largest_remainder_then_kit_order(
    started: Api, fakes: Fakes
) -> None:
    kick = (DrumHit(i / 100, "kick", 0.5) for i in range(20_001))
    snare = (DrumHit(i / 100, "snare", 0.5) for i in range(20_001))
    fakes.drums.hits = (*kick, *snare, DrumHit(1.0, "hihat", 0.5))

    job = _done(started, _post(started, drums_body(("kick", "snare", "hihat"))))

    pieces = [h["piece"] for h in job["result"]["hits"]]
    counts = {p: pieces.count(p) for p in ("kick", "snare", "hihat")}
    assert counts == {"kick": 20_000, "snare": 19_999, "hihat": 1}


def test_a_drum_transcription_error_fails_the_job_with_its_text(started: Api, fakes: Fakes) -> None:
    fakes.drums.error = TranscriptionError("The drum stems could not be read.")

    job = _done(started, _post(started, drums_body()))

    assert (job["status"], job["error"]) == ("failed", "The drum stems could not be read.")


def test_drum_hits_report_progress_while_running(started: Api, fakes: Fakes) -> None:
    fakes.drums.gate.clear()
    job_id = _post(started, drums_body())
    assert fakes.drums.entered.wait(WAIT)

    job = started.client.get(f"{JOBS}/{job_id}").json()

    assert (job["status"], job["stage"]) == ("running", "analysing")
    assert 0.05 < job["progress"] < 0.95
    fakes.drums.gate.set()
    _done(started, job_id)


# ------------------------------------------------------------------ vocal pitch


def test_a_vocal_pitch_job_returns_the_reference_melody(started: Api, fakes: Fakes) -> None:
    job = _done(started, _post(started, pitch_body()))

    result = job["result"]
    assert (job["status"], result["kind"], result["source"]) == ("completed", "vocal-pitch",
                                                                 "vocals.lead")  # fmt: skip
    assert result["frames"] == [0, 6900, 6950] and result["hopMs"] == 10
    root = started.settings.render.library_dir
    assert root is not None
    assert fakes.mixer.calls == [(((root / LEAD).resolve(),), PITCH_SAMPLE_RATE)]
    audio, source = fakes.pitch.calls[0]
    assert len(audio) == PITCH_SAMPLE_RATE // 2 and source == "vocals.lead"


def test_a_silent_vocal_stem_fails_with_the_fixed_text(started: Api, fakes: Fakes) -> None:
    fakes.pitch.error = NoMelodyError()

    job = _done(started, _post(started, pitch_body(input=stem("vocals"), source="vocals")))

    assert (job["status"], job["error"]) == ("failed", NO_MELODY_TEXT)


def test_a_vocal_stem_removed_before_the_job_runs_fails_it_readably(
    started: Api, fakes: Fakes
) -> None:
    fakes.pitch.gate.clear()
    first = _post(started, pitch_body())
    assert fakes.pitch.entered.wait(WAIT)
    second = _post(started, pitch_body(input=stem("vocals"), source="vocals"))
    root = started.settings.render.library_dir
    assert root is not None
    (root / stem("vocals")).unlink()
    fakes.pitch.gate.set()

    job = _done(started, second)

    assert (job["status"], job["error"]) == ("failed", f"Input not found: {stem('vocals')}")
    assert len(fakes.pitch.calls) == 1
    assert _done(started, first)["status"] == "completed"


def test_the_pitch_engine_of_the_analyzer_is_recorded(started: Api, fakes: Fakes) -> None:
    fakes.pitch.engine_name = "yin"

    job = _done(started, _post(started, pitch_body()))

    assert job["result"]["engine"] == "yin"
    assert started.client.get("/health").json()["instrumentAnalysis"]["pitchEngine"] == "yin"


def test_vocal_pitch_reports_progress_while_running(started: Api, fakes: Fakes) -> None:
    fakes.pitch.gate.clear()
    job_id = _post(started, pitch_body())
    assert fakes.pitch.entered.wait(WAIT)

    job = started.client.get(f"{JOBS}/{job_id}").json()

    assert job["stage"] == "analysing" and 0.1 < job["progress"] < 0.95
    fakes.pitch.gate.set()
    _done(started, job_id)


# ------------------------------------------------------------------ validation and paths


@pytest.mark.parametrize(
    "body",
    [
        {"kind": "melody", "input": LEAD},
        chords_body(inputs=[stem(n) for n in ("bass", "guitar", "piano", "other", "vocals")]),
        chords_body(inputs=[]),
        chords_body(vocabulary="jazz"),
        chords_body(engine="madmom"),
        chords_body(extra=1),
        {"kind": "drum-hits", "drumInputs": {}},
        pitch_body(source="drums"),
        {"kind": "vocal-pitch", "inputs": [LEAD], "source": "vocals"},
    ],
)
def test_invalid_bodies_are_422(started: Api, body: dict[str, Any]) -> None:
    response = started.client.post(JOBS, json=body)

    assert response.status_code == 422
    assert_conforms(response, JOBS, "post")


@pytest.mark.parametrize(
    "body",
    [
        chords_body(inputs=["../../etc/passwd"]),
        chords_body(bassInput="tracks/x/stems/bass.flac"),
        drums_body(()) | {"drumInputs": {"kick": "/abs/kick.flac"}},
        pitch_body(input="tracks/../../secret.flac"),
    ],
)
def test_paths_outside_the_allow_list_are_400(started: Api, body: dict[str, Any]) -> None:
    response = started.client.post(JOBS, json=body)

    assert response.status_code == 400
    assert response.json()["type"].endswith("invalid-input-path")
    assert_conforms(response, JOBS, "post")


def test_a_missing_input_file_is_404(started: Api) -> None:
    missing = "tracks/11111111-2222-4333-8444-555555555555/stems/piano.flac"

    response = started.client.post(JOBS, json=chords_body(inputs=[missing]))

    assert response.status_code == 404
    assert response.json()["type"].endswith("input-not-found")
    assert_conforms(response, JOBS, "post")


def test_a_job_queued_while_the_library_is_unreadable_fails_when_it_runs(
    make_api: ApiFactory, fakes: Fakes
) -> None:
    api = make_api(overrides=AppOverrides(instrument_analysis=fakes.parts()))
    library = api.settings.render.library_dir
    assert library is not None
    library.rmdir()

    job = _done(api, _post(api, pitch_body()))

    assert job["status"] == "failed" and job["error"]
    assert fakes.pitch.calls == []


# ------------------------------------------------------------------ read and delete


def test_an_unknown_job_is_404(started: Api) -> None:
    response = started.client.get(f"{JOBS}/{'0' * 32}")

    assert response.status_code == 404
    assert_conforms(response, ONE)


def test_delete_removes_the_job_and_is_idempotent(started: Api) -> None:
    job_id = _post(started, chords_body())
    _done(started, job_id)

    assert started.client.delete(f"{JOBS}/{job_id}").status_code == 204
    assert started.client.delete(f"{JOBS}/{job_id}").status_code == 204
    assert started.client.get(f"{JOBS}/{job_id}").status_code == 404


def test_delete_with_a_malformed_id_is_400(started: Api) -> None:
    assert started.client.delete(f"{JOBS}/not-an-id").status_code == 400


@pytest.mark.parametrize("kind", ["chords", "drums", "pitch"])
def test_deleting_a_running_job_stops_it(started: Api, fakes: Fakes, kind: str) -> None:
    gate: Any = getattr(fakes, kind)
    bodies: dict[str, Callable[[], dict[str, Any]]] = {
        "chords": chords_body,
        "drums": drums_body,
        "pitch": pitch_body,
    }
    gate.gate.clear()
    job_id = _post(started, bodies[kind]())
    assert gate.entered.wait(WAIT)

    assert started.client.delete(f"{JOBS}/{job_id}").status_code == 204
    gate.gate.set()

    assert _service(started).join(WAIT)
    assert started.client.get(f"{JOBS}/{job_id}").status_code == 404


# ------------------------------------------------------------------ health


def test_health_reports_the_engines_models_and_lane(started: Api) -> None:
    response = started.client.get("/health")

    assert_conforms(response, "/health")
    body = response.json()
    assert body["instrumentAnalysis"] == {
        "models": {"btc-majmin": "ready", "btc-large-voca": "not-downloaded"},
        "chordEngine": "btc",
        "pitchEngine": "pyin",
    }
    assert body["lanes"]["instrumentAnalysis"] == {"queued": 0, "running": False}


def test_the_lane_store_and_sweep_are_registered(started: Api) -> None:
    runtime = started.client.app.state.runtime  # type: ignore[attr-defined]

    assert _service(started).sweep in runtime.lanes.sweeper._tasks
    assert (started.settings.data_dir / "instrument-analysis-jobs").is_dir()


def test_the_chords_mix_rate_constant_is_the_btc_rate() -> None:
    assert CHORD_SAMPLE_RATE == 22_050
