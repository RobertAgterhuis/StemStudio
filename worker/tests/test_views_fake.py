"""Tests of the fake instrument analyses of the E2E stack (016 T031, research R-11, data-model 2.7).

Chords: one chord per bar on the request grid (120 BPM from 0.5 s without one), ``C G Am F`` for
bars 1-8 and ``D A Bm G`` from bar 9 (``full``: sevenths and slash chords), key C major with a
D major region from bar 9. Drum hits: the 008 fake groove at 120 BPM from 0.5 s plus a crash on
beat 1 of every fourth bar. Melody: one note per beat (120 BPM from 0.5 s) walking
``60 62 64 65 67 69 71 72 71 69 67 65 64 62`` by the global beat index, voiced only inside the
fake lyric lines. Everything is deterministic.
"""

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.jobs.instrument_analysis_ports import ChordInputs
from stemstudio_worker.lyrics.fake import FAKE_LINES
from stemstudio_worker.lyrics.interpolate import line_end_ms
from stemstudio_worker.ports import PITCH_SAMPLE_RATE, AnalysisError
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    AnalysisWarning,
    BeatGridTempo,
    ChordsJobRequest,
    ChordVocabulary,
)
from stemstudio_worker.views_fake import (
    FAKE_WALK,
    FakeChordAnalyzer,
    FakeDrumHitsTranscriber,
    FakePitchAnalyzer,
)

STEM = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/{}.flac"


def _wav(path: Path, seconds: float) -> Path:
    sf.write(path, np.zeros(int(seconds * 8000), dtype=np.float32), 8000)
    return path


def _request(
    vocabulary: ChordVocabulary = "simple", grid: BeatGridTempo | None = None
) -> ChordsJobRequest:
    return ChordsJobRequest(
        kind="chords", inputs=[STEM.format("guitar")], grid=grid, vocabulary=vocabulary
    )


def _grid() -> BeatGridTempo:
    """100 BPM in 4/4 with the first downbeat at 1.0 s and two downbeats given."""
    beats = [1.0 + 0.6 * i for i in range(8)]
    return BeatGridTempo(bpm=100, beats_per_bar=4, beats=beats, downbeats=[1.0, 3.4])


def _chart(
    tmp_path: Path,
    seconds: float,
    vocabulary: ChordVocabulary = "simple",
    grid: BeatGridTempo | None = None,
):  # type: ignore[no-untyped-def]
    files = ChordInputs(
        harmonic=(_wav(tmp_path / "a.wav", seconds), _wav(tmp_path / "b.wav", min(seconds, 1.0)))
    )
    seen: list[float] = []
    outcome = FakeChordAnalyzer().analyze(_request(vocabulary, grid), files, seen.append)
    assert seen == [1.0]
    assert outcome.device is None
    return outcome.chart


def test_chords_without_a_grid_change_every_bar_of_120_bpm_from_half_a_second(
    tmp_path: Path,
) -> None:
    chart = _chart(tmp_path, 40.0)

    labels = [s.label for s in chart.segments]
    assert labels[:10] == [
        "N",
        "C:maj",
        "G:maj",
        "A:min",
        "F:maj",
        "C:maj",
        "G:maj",
        "A:min",
        "F:maj",
        "D:maj",
    ]
    assert labels[10:13] == ["A:maj", "B:min", "G:maj"]
    assert [(s.start_ms, s.end_ms) for s in chart.segments[:3]] == [
        (0, 500),
        (500, 2500),
        (2500, 4500),
    ]
    assert chart.segments[-1].end_ms == 40_000
    assert all(s.confidence == 0.9 for s in chart.segments)
    assert (chart.engine, chart.vocabulary, chart.beat_snapped, chart.no_chords) == (
        "fake",
        "simple",
        False,
        False,
    )


def test_chords_carry_root_quality_and_bass(tmp_path: Path) -> None:
    chart = _chart(tmp_path, 20.0)

    first, g, a_min = chart.segments[1], chart.segments[2], chart.segments[3]
    assert (first.root, first.quality, first.bass) == (0, "maj", None)
    assert (g.root, g.quality) == (7, "maj")
    assert (a_min.root, a_min.quality) == (9, "min")
    assert chart.segments[0].root is None and chart.segments[0].quality is None


def test_full_vocabulary_uses_sevenths_and_a_slash_chord(tmp_path: Path) -> None:
    chart = _chart(tmp_path, 40.0, "full")

    labels = [s.label for s in chart.segments]
    assert labels[1:5] == ["C:maj7", "G:7", "A:min7", "F:maj/3"]
    assert labels[9:13] == ["D:maj7", "A:7", "B:min7", "G:maj/3"]
    f_slash, g_slash = chart.segments[4], chart.segments[12]
    assert (f_slash.root, f_slash.quality, f_slash.bass) == (5, "maj", 9)
    assert (g_slash.root, g_slash.bass) == (7, 11)
    assert chart.vocabulary == "full"


def test_the_key_is_c_major_with_a_d_major_region_from_bar_nine(tmp_path: Path) -> None:
    chart = _chart(tmp_path, 40.0)

    assert chart.key is not None
    assert (chart.key.tonic, chart.key.mode, chart.key.confidence) == (0, "major", 0.9)
    regions = [(r.start_ms, r.end_ms, r.tonic, r.mode) for r in chart.key_regions]
    assert regions == [(500, 16_500, 0, "major"), (16_500, 40_000, 2, "major")]
    assert chart.prefer_flats is False
    assert chart.warnings == []


def test_a_short_song_has_one_key_region(tmp_path: Path) -> None:
    chart = _chart(tmp_path, 10.0)

    assert [(r.start_ms, r.end_ms, r.tonic) for r in chart.key_regions] == [(500, 10_000, 0)]
    assert chart.segments[-1].end_ms == 10_000


def test_bars_follow_the_grid_downbeats_and_continue_with_its_tempo(tmp_path: Path) -> None:
    chart = _chart(tmp_path, 12.0, grid=_grid())

    spans = [(s.start_ms, s.end_ms, s.label) for s in chart.segments]
    assert spans[:4] == [
        (0, 1000, "N"),
        (1000, 3400, "C:maj"),
        (3400, 5800, "G:maj"),
        (5800, 8200, "A:min"),
    ]
    assert spans[-1][1] == 12_000
    assert chart.beat_snapped is True


def test_a_grid_downbeat_at_zero_gives_no_leading_no_chord(tmp_path: Path) -> None:
    grid = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[0.0, 0.5], downbeats=[0.0])

    chart = _chart(tmp_path, 5.0, grid=grid)

    assert chart.segments[0].label == "C:maj" and chart.segments[0].start_ms == 0


def test_audio_shorter_than_the_first_downbeat_is_all_no_chord(tmp_path: Path) -> None:
    chart = _chart(tmp_path, 0.4)

    assert [s.label for s in chart.segments] == ["N"]
    assert chart.no_chords is True and chart.key is None and chart.key_regions == []


def test_an_unreadable_input_fails_readably(tmp_path: Path) -> None:
    broken = tmp_path / "broken.flac"
    broken.write_bytes(b"not audio")

    with pytest.raises(AnalysisError, match="could not be read"):
        FakeChordAnalyzer().analyze(_request(), ChordInputs(harmonic=(broken,)), lambda _f: None)


def test_the_fake_chord_analyzer_reports_the_fake_engine_and_no_models(tmp_path: Path) -> None:
    analyzer = FakeChordAnalyzer()

    assert analyzer.default_engine == "fake"
    assert analyzer.models().btc_majmin == "not-downloaded"


# ------------------------------------------------------------------ drum hits


def _hits(tmp_path: Path) -> dict[str, list[float]]:
    files = {
        piece: _wav(tmp_path / f"{piece}.wav", 20.0)
        for piece in ("kick", "snare", "hihat", "cymbals")
    }
    hits = FakeDrumHitsTranscriber().transcribe(files, None, lambda _f: None)
    times: dict[str, list[float]] = {"all": [h.time for h in hits]}
    for hit in hits:
        times.setdefault(hit.piece, []).append(hit.time)
    return times


def test_drum_hits_play_the_008_groove_from_half_a_second(tmp_path: Path) -> None:
    times = _hits(tmp_path)

    assert times["kick"][:3] == [0.5, 1.5, 2.5]
    assert times["snare"][:2] == [1.0, 2.0]
    assert times["hihat"][:3] == [0.5, 0.75, 1.0]
    assert min(times["all"]) == 0.5
    assert times["all"] == sorted(times["all"])


def test_drum_hits_add_a_crash_every_fourth_bar(tmp_path: Path) -> None:
    assert _hits(tmp_path)["cymbals"] == [0.5, 8.5, 16.5]


def test_drum_hit_progress_ends_at_one_and_the_engine_is_fake(tmp_path: Path) -> None:
    seen: list[float] = []

    FakeDrumHitsTranscriber().transcribe({"kick": _wav(tmp_path / "k.wav", 2.0)}, None, seen.append)

    assert seen[-1] == 1.0
    assert FakeDrumHitsTranscriber().engine == "fake"


def test_without_a_cymbals_input_there_is_no_crash(tmp_path: Path) -> None:
    hits = FakeDrumHitsTranscriber().transcribe(
        {"kick": _wav(tmp_path / "k.wav", 10.0)}, None, lambda _f: None
    )

    assert {h.piece for h in hits} == {"kick"}


def test_no_drum_input_gives_no_hits() -> None:
    assert FakeDrumHitsTranscriber().transcribe({}, None, lambda _f: None) == ()


# ------------------------------------------------------------------ reference melody


def _lines(seconds: float) -> list[tuple[int, int]]:
    step = round(seconds * 1000) // (len(FAKE_LINES) + 1)
    starts = [step * (i + 1) for i in range(len(FAKE_LINES))]
    nexts = [*starts[1:], None]
    return [(s, line_end_ms(t, s, n)) for s, t, n in zip(starts, FAKE_LINES, nexts, strict=True)]


def test_the_walk_is_the_manifest_melody() -> None:
    assert FAKE_WALK == (60, 62, 64, 65, 67, 69, 71, 72, 71, 69, 67, 65, 64, 62)


def test_the_melody_is_voiced_only_inside_the_fake_lyric_lines(tmp_path: Path) -> None:
    seconds = 30.0
    audio = np.zeros(int(seconds * PITCH_SAMPLE_RATE), dtype=np.float32)
    seen: list[float] = []

    pitch = FakePitchAnalyzer().analyze(audio, "vocals", seen.append)

    assert len(pitch.frames) == 3000 and pitch.hop_ms == 10
    lines = _lines(seconds)
    for index, cents in enumerate(pitch.frames):
        t = index * 10
        inside = any(start <= t < end for start, end in lines)
        assert (cents > 0) == (inside and t >= 500), t


def test_each_beat_sings_the_walk_note_of_its_global_index() -> None:
    audio = np.zeros(30 * PITCH_SAMPLE_RATE, dtype=np.float32)

    pitch = FakePitchAnalyzer().analyze(audio, "vocals", lambda _f: None)

    start = _lines(30.0)[0][0]
    beat = (start - 500) // 500
    assert pitch.frames[start // 10] == FAKE_WALK[beat % 14] * 100
    later = start + 1000
    assert pitch.frames[later // 10] == FAKE_WALK[(beat + 2) % 14] * 100


def test_the_melody_summary_has_the_fake_engine_ratio_and_range() -> None:
    audio = np.zeros(30 * PITCH_SAMPLE_RATE, dtype=np.float32)
    seen: list[float] = []

    pitch = FakePitchAnalyzer().analyze(audio, "vocals", seen.append)

    assert (pitch.engine, pitch.source, pitch.warnings) == ("fake", "vocals", [])
    assert 0 < pitch.voiced_ratio < 1
    assert pitch.range is not None and 6000 <= pitch.range.low_cents <= pitch.range.high_cents
    assert seen[-1] == 1.0


def test_the_global_beat_index_does_not_restart_per_line(tmp_path: Path) -> None:
    audio = np.zeros(60 * PITCH_SAMPLE_RATE, dtype=np.float32)

    pitch = FakePitchAnalyzer().analyze(audio, "vocals", lambda _f: None)

    second = _lines(60.0)[1][0]
    beat = (second - 500) // 500
    assert pitch.frames[second // 10] == FAKE_WALK[beat % 14] * 100


def test_a_lead_vocal_detail_stem_gets_the_detail_bleed_warning() -> None:
    audio = np.zeros(10 * PITCH_SAMPLE_RATE, dtype=np.float32)

    pitch = FakePitchAnalyzer().analyze(audio, "vocals.lead", lambda _f: None)

    text = ANALYSIS_WARNING_TEXTS["detail-bleed"]
    assert pitch.warnings == [AnalysisWarning(code="detail-bleed", text=text)]
    assert pitch.source == "vocals.lead"


def test_a_very_short_stem_has_no_voiced_frame_and_fails_as_silent() -> None:
    from stemstudio_worker.ports import NoMelodyError  # noqa: PLC0415

    with pytest.raises(NoMelodyError):
        FakePitchAnalyzer().analyze(np.zeros(160, dtype=np.float32), "vocals", lambda _f: None)


def test_the_fake_pitch_engine_is_fake() -> None:
    assert FakePitchAnalyzer().engine == "fake"
