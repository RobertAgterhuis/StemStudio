"""Tests of the section and channel display names (data-model 2.3)."""

import pytest

from stemstudio_worker.analysis.structure.names import channel_name, numbered_labels, solo_label


@pytest.mark.parametrize(
    ("channel", "name"),
    [("guitar", "Guitar"), ("guitar.electric-guitar", "Electric guitar"),
     ("other.woodwinds", "Woodwinds"), ("drums.hihat", "Hi-hat"), ("piano.new-thing", "New thing"),
     ("theremin", "Theremin")],
)  # fmt: skip
def test_channel_names_follow_the_004_labels(channel: str, name: str) -> None:
    assert channel_name(channel) == name


def test_solo_labels() -> None:
    assert (solo_label(None), solo_label("bass")) == ("Solo", "Bass solo")


def test_labels_are_numbered_per_kind_and_parts_by_cluster() -> None:
    labels = numbered_labels(
        ["intro", "other", "other", "solo", "solo", "bridge"],
        ["A", "B", "C", "D", "D", "E"],
        [None, None, None, "guitar", "piano", None],
    )

    assert labels == ["Intro", "Part B", "Part C", "Guitar solo 1", "Piano solo 2", "Bridge"]
