"""Tests of the stable-ts word mapping and the collapse check (006 T040, research.md P0-2)."""

import pytest

from stemstudio_worker.alignment.mapping import AlignedWord, build_document, is_collapsed
from stemstudio_worker.ports import AlignmentError
from stemstudio_worker.schemas import ALIGNMENT_FAILED_TEXT, TimedLyrics

from .fakes import TEXT, collapsed, spread


def _words(result: object) -> list[AlignedWord]:
    return [AlignedWord(w.word, w.start, w.end, w.probability) for w in result.all_words()]  # type: ignore[attr-defined]


def _spans(document: TimedLyrics) -> list[list[tuple[str, int | None, int | None]]]:
    return [[(w.text, w.start_ms, w.end_ms) for w in line.words] for line in document.lines]


def test_words_are_mapped_back_to_our_tokens_by_order() -> None:
    document = build_document(TEXT, _words(spread()), "en")

    assert (document.sync_level, document.origin, document.source) == ("word", "aligned", "user")
    assert (document.language, document.plain_text) == ("en", TEXT)
    assert [line.text for line in document.lines] == ["Fake line one", "La la la"]
    assert _spans(document) == [
        [("Fake", 1000, 1400), ("line", 1400, 1800), ("one", 1800, 2200)],
        [("La", 2200, 2600), ("la", 2600, 3000), ("la", 3000, 3400)],
    ]
    assert [(line.start_ms, line.end_ms) for line in document.lines] == [(1000, 2200), (2200, 3400)]
    assert all(
        w.origin == "aligned" and not w.low_confidence
        for line in document.lines
        for w in line.words
    )


def test_split_tokens_are_merged_and_blank_lines_dropped() -> None:
    words = _words(spread(("Fa", "ke", "line", "one", "La", "la", "la")))

    document = build_document("Fake line one\n\n  \nLa la la\n", words, None)

    assert _spans(document)[0][0] == ("Fake", 1000, 1800)
    assert document.language is None


def test_low_probability_and_very_short_words_are_flagged() -> None:
    words = _words(spread())
    words[1] = AlignedWord(" line", 1.4, 1.8, 0.1)
    words[2] = AlignedWord(" one", 1.8, 1.81, 0.9)

    document = build_document(TEXT, words, "en")

    assert [w.low_confidence for w in document.lines[0].words] == [False, True, True]


def test_times_are_repaired_into_valid_word_spans() -> None:
    words = _words(spread())
    words[0] = AlignedWord(" Fake", 1.0, 1.0, 0.9)
    words[3] = AlignedWord(" La", 0.5, 9.0, 0.9)

    document = build_document(TEXT, words, "en")

    first, second = _spans(document)
    assert first[0] == ("Fake", 1000, 1001)
    assert second[0] == ("La", 1800, 4800)


def test_a_text_mismatch_fails_the_alignment() -> None:
    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        build_document(TEXT, _words(spread(("Other", "words"))), "en")


def test_a_document_over_the_limits_fails_the_alignment() -> None:
    text = " ".join(["la"] * 41)
    words = _words(spread(tuple(text.split()), step=0.1))

    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        build_document(text, words, "en")


def test_a_refined_document_keeps_the_given_line_times() -> None:
    refine = build_document(TEXT, _words(spread()), "en").model_copy(update={"sync_level": "line"})
    spans = [(900, 2000), (2500, 3200)]
    lines = [
        line.model_copy(update={"start_ms": s, "end_ms": e})
        for line, (s, e) in zip(refine.lines, spans, strict=True)
    ]
    refine = refine.model_copy(update={"lines": lines})

    document = build_document(TEXT, _words(spread()), "en", refine)

    assert [(line.start_ms, line.end_ms) for line in document.lines] == [(900, 2000), (2500, 3200)]
    assert _spans(document)[0][-1] == ("one", 1800, 2000)
    assert _spans(document)[1][0] == ("La", 2500, 2600)


def test_a_refine_document_with_other_lines_fails() -> None:
    refine = build_document("Fake line one", _words(spread(("Fake", "line", "one"))), "en")

    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        build_document(TEXT, _words(spread()), "en", refine)


def test_a_good_alignment_has_not_collapsed() -> None:
    assert not is_collapsed(build_document(TEXT, _words(spread()), "en"))


def test_mostly_zero_length_words_are_a_collapse() -> None:
    assert is_collapsed(build_document(TEXT, _words(collapsed()), "en"))


def test_overlapping_lines_are_a_collapse() -> None:
    text = "\n".join(["la la"] * 4)
    words = [AlignedWord(" la", 1.0 + 0.3 * i, 1.0 + 0.3 * i + 0.25, 0.9) for i in range(8)]
    overlapping = build_document(text, words, "en")
    lines = [
        line.model_copy(update={"end_ms": (line.end_ms or 0) + 700}) for line in overlapping.lines
    ]

    assert not is_collapsed(overlapping)
    assert is_collapsed(overlapping.model_copy(update={"lines": lines}))


def test_an_empty_document_has_not_collapsed() -> None:
    empty = TimedLyrics(sync_level="word", origin="aligned", source="user")

    assert not is_collapsed(empty)
