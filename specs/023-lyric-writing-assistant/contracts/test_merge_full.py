"""Tests of the block merge in merge_full.py (run: ``python -m pytest specs/023-lyric-writing-assistant/contracts``)."""

from __future__ import annotations

import merge_full
import pytest

BASE = ["  /a:", "    put:", "      one", "    delete:", "      two"]


def test_block_changed_on_both_sides_without_overlap_is_merged() -> None:
    ours = [
        "  /a:",
        "    put:",
        "      one",
        "    delete:",
        "      two",
        "      marker",
    ]
    theirs = ["  /a:", "    put:", "      one", "      409", "    delete:", "      two"]

    merged = merge_full.merge_section(
        "paths", {"/a": BASE}, {"/a": ours}, {"/a": theirs}
    )

    assert merged == [
        "  /a:",
        "    put:",
        "      one",
        "      409",
        "    delete:",
        "      two",
        "      marker",
    ]


def test_block_changed_on_both_sides_with_overlap_fails_loudly() -> None:
    ours = ["  /a:", "    put:", "      ours", "    delete:", "      two"]
    theirs = ["  /a:", "    put:", "      theirs", "    delete:", "      two"]

    with pytest.raises(SystemExit, match="/a changed on both sides"):
        merge_full.merge_section("paths", {"/a": BASE}, {"/a": ours}, {"/a": theirs})


def test_block_added_on_both_sides_differently_fails_loudly() -> None:
    with pytest.raises(SystemExit, match="/b changed on both sides"):
        merge_full.merge_section(
            "paths", {}, {"/b": ["  /b:", "    x"]}, {"/b": ["  /b:", "    y"]}
        )


def test_block_changed_on_one_side_only_keeps_that_side() -> None:
    theirs = [*BASE, "      extra"]

    assert (
        merge_full.merge_section("paths", {"/a": BASE}, {"/a": BASE}, {"/a": theirs})
        == theirs
    )
