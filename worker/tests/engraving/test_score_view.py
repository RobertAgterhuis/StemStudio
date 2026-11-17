"""Tests of the score view: SVG pages and the bar map (014 T026, data-model 2.5, SC-003)."""

import re

import pytest

from stemstudio_worker.engraving import score_view
from stemstudio_worker.engraving.score_view import VerovioScoreViewRenderer
from stemstudio_worker.ports import ScoreView, ScoreViewError, ScoreViewRenderer
from stemstudio_worker.schemas import (
    SCORE_VIEW_FAILED_TEXT,
    BeatGridTempo,
    ManualTempo,
    ScoreViewResult,
)

from .view_scores import score

STEADY = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[0.5 + 0.5 * i for i in range(400)],
                       downbeats=[0.5 + 2.0 * i for i in range(100)])  # fmt: skip


def _render(musicxml: bytes, grid: BeatGridTempo | ManualTempo = STEADY,
            duration: float = 300.0) -> ScoreView:  # fmt: skip
    return VerovioScoreViewRenderer().render(musicxml, grid, duration)


def _times(view: ScoreView) -> list[float]:
    return [t for bar in view.bars for t in (bar.start_seconds, bar.end_seconds)]


def _result(view: ScoreView) -> ScoreViewResult:
    pages = [{"page": n, "widthPx": view.page_width_px, "heightPx": view.page_height_px}
             for n in range(1, len(view.pages) + 1)]  # fmt: skip
    return ScoreViewResult.model_validate(
        {"pages": pages, "bars": [b.model_dump(by_alias=True) for b in view.bars],
         "engine": {"name": "verovio", "version": view.engine_version}}
    )  # fmt: skip


@pytest.mark.parametrize("kind", ["single", "grand", "drums"])
@pytest.mark.parametrize("bars", [4, 37])
def test_every_bar_gets_one_box_inside_its_page(kind: str, bars: int) -> None:
    view = _render(score(bars, kind))

    assert len(view.bars) == bars
    assert [b.index for b in view.bars] == list(range(bars))
    assert [b.number for b in view.bars] == list(range(1, bars + 1))
    for bar in view.bars:
        assert 1 <= bar.page <= len(view.pages)
        assert bar.x >= 0 and bar.x + bar.width <= 1
        assert bar.y >= 0 and bar.y + bar.height <= 1
    _result(view)


def test_a_long_score_has_several_pages_in_order() -> None:
    view = _render(score(150))

    pages = [bar.page for bar in view.bars]
    assert len(view.pages) >= 2
    assert pages == sorted(pages)
    assert set(pages) == set(range(1, len(view.pages) + 1))
    assert (view.page_width_px, view.page_height_px) == (840, 1188)


def test_grand_staff_boxes_span_both_staves() -> None:
    single = _render(score(4, "single")).bars[1]
    grand = _render(score(4, "grand")).bars[1]

    assert grand.height > 1.8 * single.height


def test_bar_times_follow_an_uneven_beat_grid() -> None:
    beats = [0.5, 1.0, 1.6, 2.1, 2.5, 3.0, 3.4, 4.0, 4.6, 5.0, 5.5, 6.0]
    grid = BeatGridTempo(bpm=120, beats_per_bar=4, beats=beats, downbeats=[0.5, 2.5, 4.6])

    view = _render(score(4), grid, duration=8.3)

    assert _times(view) == pytest.approx([0.5, 2.5, 2.5, 4.6, 4.6, 6.5, 6.5, 8.3])


def test_bar_times_of_a_manual_tempo() -> None:
    grid = ManualTempo(bpm=100, beats_per_bar=4, first_downbeat_seconds=1.0)

    view = _render(score(3), grid, duration=8.5)

    assert _times(view) == pytest.approx([1.0, 3.4, 3.4, 5.8, 5.8, 8.5])


def test_a_pickup_starts_before_the_first_downbeat() -> None:
    grid = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[2.0 + 0.5 * i for i in range(30)],
                         downbeats=[2.0 + 2.0 * i for i in range(8)])  # fmt: skip

    view = _render(score(3, pickup_beats=1), grid, duration=9.0)

    assert [b.number for b in view.bars] == [0, 1, 2, 3]
    assert _times(view) == pytest.approx([1.5, 2.0, 2.0, 4.0, 4.0, 6.0, 6.0, 9.0])


def test_a_bar_past_the_duration_still_has_a_positive_length() -> None:
    view = _render(score(4), STEADY, duration=3.0)

    last = view.bars[-1]
    assert last.end_seconds > last.start_seconds
    _result(view)


def test_pages_using_leipzig_text_embed_the_font_and_nothing_else_is_added() -> None:
    view = _render(score(150))

    assert "@font-face" in view.pages[0]  # the tempo mark is Leipzig text
    for page in view.pages:
        uses_leipzig = 'font-family="Leipzig"' in page
        assert uses_leipzig <= ("@font-face" in page)
        assert page.count("@font-face") <= 1
    assert not any("@font-face" in page for page in _render(score(4, tempo=False)).pages)


def test_pages_are_safe_as_images() -> None:
    for page in _render(score(8, "grand")).pages:
        assert "<script" not in page
        assert "foreignObject" not in page
        assert not re.search(r'href="(?!#)', page)
        assert "bounding-box" not in page


def test_the_engine_version_is_verovios() -> None:
    assert _render(score(2)).engine_version.startswith("6.4.0")


@pytest.mark.parametrize(
    "data",
    [b"\xff\xfe not utf-8", b"<html><body>no score</body></html>", b"", b"<score-partwise"],
)
def test_unreadable_input_fails_with_the_fixed_text(data: bytes) -> None:
    with pytest.raises(ScoreViewError, match=re.escape(SCORE_VIEW_FAILED_TEXT)):
        _render(data)


def test_too_many_bars_or_pages_fail(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(score_view, "MAX_SCORE_BARS", 3)
    with pytest.raises(ScoreViewError):
        _render(score(4))
    monkeypatch.setattr(score_view, "MAX_SCORE_BARS", 2000)
    monkeypatch.setattr(score_view, "MAX_SCORE_PAGES", 1)
    with pytest.raises(ScoreViewError):
        _render(score(150))


def test_unsafe_svg_from_the_engraver_is_refused() -> None:
    with pytest.raises(ScoreViewError):
        score_view.check_page("<svg><script>alert(1)</script></svg>")
    with pytest.raises(ScoreViewError):
        score_view.check_page('<svg><a href="https://example.org/x">x</a></svg>')
    with pytest.raises(ScoreViewError):
        score_view.check_page('<svg><text font-family="Leipzig">x</text></svg>')
    score_view.check_page('<svg><use xlink:href="#E062"/></svg>')


def test_it_is_a_score_view_renderer() -> None:
    assert isinstance(VerovioScoreViewRenderer(), ScoreViewRenderer)


@pytest.mark.parametrize(
    "data",
    [b'<mei xmlns="http://www.music-encoding.org/ns/mei"/>', b"**kern\n4c\n*-\n", b"X:1\nK:C\nC|"],
)
def test_other_notation_formats_are_refused(data: bytes) -> None:
    with pytest.raises(ScoreViewError):
        _render(data)


def test_a_grand_staff_pickup_counts_the_longest_voice() -> None:
    grid = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[2.0 + 0.5 * i for i in range(30)],
                         downbeats=[2.0 + 2.0 * i for i in range(8)])  # fmt: skip

    view = _render(score(2, "grand", pickup_beats=2), grid, duration=7.0)

    assert view.bars[0].start_seconds == pytest.approx(1.0)


def test_bounding_box_pages_must_be_svg_with_staff_boxes() -> None:
    with pytest.raises(ScoreViewError):
        score_view.measure_boxes("<svg", 1)
    empty = '<svg xmlns="http://www.w3.org/2000/svg"><g class="measure"/></svg>'
    with pytest.raises(ScoreViewError):
        score_view.measure_boxes(empty, 1)
    no_margin = ('<svg xmlns="http://www.w3.org/2000/svg"><g class="measure">'
                 '<g class="staff bounding-box"><rect x="2100" y="0" width="2100" height="2970"/>'
                 "</g></g></svg>")  # fmt: skip
    assert score_view.measure_boxes(no_margin, 2) == [score_view.Box(2, 0.1, 0.0, 0.1, 0.1)]


class _RefusingToolkit:
    def setOptions(self, options: dict[str, object]) -> bool:  # noqa: N802 - Verovio API
        return True

    def loadData(self, data: str) -> bool:  # noqa: N802 - Verovio API
        return False


def test_a_score_verovio_cannot_load_fails(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(score_view, "new_toolkit", _RefusingToolkit)

    with pytest.raises(ScoreViewError):
        _render(score(2))


def test_a_page_margin_without_a_translate_adds_nothing() -> None:
    page = ('<svg xmlns="http://www.w3.org/2000/svg"><g class="page-margin"><g class="measure">'
            '<g class="staff bounding-box"><rect x="0" y="0" width="2100" height="2970"/></g>'
            "</g></g></svg>")  # fmt: skip

    assert score_view.measure_boxes(page, 1)[0].x == 0.0
