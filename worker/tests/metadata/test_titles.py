"""005:T022: YouTube title cleaning and comparison normalisation (research R-04, ADR-014 D4)."""

from typing import Any

import pytest

from stemstudio_worker.metadata.titles import (
    clean_artist,
    clean_title,
    normalize_for_compare,
    youtube_lookup_names,
)


@pytest.mark.parametrize(
    ("title", "expected"),
    [
        ("Hero Of The Day (Official Music Video)", "Hero Of The Day"),
        ("Hero Of The Day [Official Video]", "Hero Of The Day"),
        ("Hero Of The Day (official audio)", "Hero Of The Day"),
        ("Hero Of The Day (Lyric Video)", "Hero Of The Day"),
        ("Hero Of The Day (Lyrics Video)", "Hero Of The Day"),
        ("Hero Of The Day [Lyrics]", "Hero Of The Day"),
        ("Hero Of The Day (Audio)", "Hero Of The Day"),
        ("Hero Of The Day [HD]", "Hero Of The Day"),
        ("Hero Of The Day (HQ)", "Hero Of The Day"),
        ("Hero Of The Day [4K]", "Hero Of The Day"),
        ("Hero Of The Day (Remastered 2021)", "Hero Of The Day"),
        ("Hero Of The Day (Remaster)", "Hero Of The Day"),
        ("Hero Of The Day - Remastered 2009", "Hero Of The Day"),
        ("Hero Of The Day - Official Music Video", "Hero Of The Day"),
        ("Hero Of The Day (Official Video) [HD]", "Hero Of The Day"),
        ("Hero Of The Day (feat. Someone Else)", "Hero Of The Day"),
        ("Hero Of The Day [Feat. Someone]", "Hero Of The Day"),
        ("Hero Of The Day ft. Someone", "Hero Of The Day"),
        ("Hero Of The Day feat. Someone (Official Video)", "Hero Of The Day"),
        ("Hero Of The Day (Live at Wembley)", "Hero Of The Day (Live at Wembley)"),
        ("Hero Of The Day - Live", "Hero Of The Day - Live"),
        ("Audioslave", "Audioslave"),
        ("Lyrical Song", "Lyrical Song"),
        ("  Spaced   Out  ", "Spaced Out"),
        ("(Official Video)", "(Official Video)"),
        ("東京事変 (Official Music Video)", "東京事変"),
    ],
)
def test_clean_title(title: str, expected: str) -> None:
    assert clean_title(title) == expected


@pytest.mark.parametrize(
    ("artist", "expected"),
    [
        ("Kevin MacLeod - Topic", "Kevin MacLeod"),
        ("MetallicaVEVO", "Metallica"),
        ("Metallica VEVO", "Metallica"),
        ("Metallica Official", "Metallica"),
        ("Metallica", "Metallica"),
        ("Official", "Official"),
    ],
)
def test_clean_artist(artist: str, expected: str) -> None:
    assert clean_artist(artist) == expected


@pytest.mark.parametrize(
    ("info", "expected"),
    [
        (
            {
                "track": "Hero of the Day",
                "artist": "Metallica",
                "title": "Metallica - Hero Of The Day (Official Music Video)",
                "uploader": "Metallica",
            },
            ("Hero of the Day", "Metallica"),
        ),
        (
            {"title": "Metallica - Hero Of The Day (Official Music Video)", "uploader": "Fan"},
            ("Hero Of The Day", "Metallica"),
        ),
        (
            {"title": "Kevin MacLeod \u2013 Monkeys Spinning Monkeys", "uploader": "Fan"},
            ("Monkeys Spinning Monkeys", "Kevin MacLeod"),
        ),
        (
            {"title": "Monkeys Spinning Monkeys", "uploader": "Kevin MacLeod - Topic"},
            ("Monkeys Spinning Monkeys", "Kevin MacLeod"),
        ),
        (
            {"title": "Monkeys Spinning Monkeys", "channel": "Kevin MacLeod"},
            ("Monkeys Spinning Monkeys", "Kevin MacLeod"),
        ),
        ({"title": "Some Song [HD]"}, ("Some Song", None)),
        ({"track": "Only Track", "title": "x - y"}, ("y", "x")),
        ({"title": "   ", "uploader": "Someone"}, (None, None)),
        ({}, (None, None)),
        ({"title": 42, "uploader": ["x"]}, (None, None)),
        ({"title": "A - B - C", "uploader": "U"}, ("B - C", "A")),
        ({"title": " - Song", "uploader": "U"}, ("- Song", "U")),
    ],
)
def test_youtube_lookup_names(
    info: dict[str, Any], expected: tuple[str | None, str | None]
) -> None:
    assert youtube_lookup_names(info) == expected


def test_lookup_names_are_cut_to_500_characters() -> None:
    title, artist = youtube_lookup_names({"title": "x" * 900, "uploader": "y" * 900})

    assert title is not None
    assert artist is not None
    assert (len(title), len(artist)) == (500, 500)


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("Hero Of The Day", "hero of the day"),
        ("Rock & Roll", "rock and roll"),
        ("Don't Stop Me Now!", "dont stop me now"),
        ("AC/DC", "acdc"),
        ("  Multiple   spaces\there ", "multiple spaces here"),
        ("\uff26\uff55\uff4c\uff4c", "full"),
        ("Straße", "strasse"),
        ("东京事变", "东京事变"),
        ("Beyoncé", "beyoncé"),
        ("...", ""),
    ],
)
def test_normalize_for_compare(text: str, expected: str) -> None:
    assert normalize_for_compare(text) == expected
