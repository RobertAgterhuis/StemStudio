"""Features 022 and 023: what the gate measures and what it leaves out (T089, T071).

Library folders (022) and the lyric writing assistant (023) are new folders inside the existing
projects. They are measured like all production code, get one report row
each (a sub-row of the project rows, like ``Api/Agent/Secrets``) and the vendored dictionaries of
the assistant (Hunspell ``.dic``/``.aff``, CMUdict; licences in ``PROVENANCE.md``) and the gold
lyric fixtures of the evaluation are data, not code: neither SIG nor jscpd analyse them.
"""

import json

import pytest

import duplication
from common import is_excluded, owner_of
from components import COMPONENTS, Component

DICTIONARIES = "src/StemStudio.Api/LyricAssist/Dictionaries/"


# ------------------------------------------------------------------------------- exclusions


@pytest.mark.parametrize(
    "path",
    [
        "src/StemStudio.Api/LyricAssist/LyricAssistEndpoints.cs",
        "src/StemStudio.Api/Library/LibraryFiler.cs",
        "src/StemStudio.Domain/Lyrics/Assist/RhymeFinder.cs",
        "tests/StemStudio.Api.Tests/LyricAssist/LyricAssistServiceTests.cs",
    ],
)
def test_gate_still_measures_the_feature_code(path: str) -> None:
    assert not is_excluded(path)


def test_gate_and_jscpd_skip_the_vendored_dictionaries() -> None:
    config = json.loads(duplication.CONFIG_PATH.read_text(encoding="utf-8"))
    assert "**/LyricAssist/Dictionaries/**" in config["ignore"]
    assert is_excluded(DICTIONARIES + "nl/nl.dic")
    assert is_excluded(DICTIONARIES + "cmudict/cmudict.dict")
    assert not is_excluded("src/StemStudio.Api/LyricAssist/HunspellSpellChecker.cs")


# ------------------------------------------------------------------------------- report rows

LIBRARY = (
    "src/StemStudio.Domain/Library/PlacementResolver.cs",
    "src/StemStudio.Api/Library/LibraryFiler.cs",
    "src/StemStudio.Web/Library/LiveSearchController.cs",
    "src/StemStudio.Web/Components/Library/FolderTile.razor.cs",
    "src/StemStudio.Agent/Tools/Providers/LibraryToolProvider.cs",
)
LYRIC_ASSIST = (
    "src/StemStudio.Domain/Lyrics/Assist/RhymeFinder.cs",
    "src/StemStudio.Api/LyricAssist/LyricAssistService.cs",
    "src/StemStudio.Agent/LyricAssist/LyricAssistant.cs",
    "src/StemStudio.Agent/Tools/Providers/LyricAssistToolProvider.cs",
    "src/StemStudio.Web/Components/Lyrics/Assist/AssistPane.razor.cs",
)
NOT_FEATURE = (
    "src/StemStudio.Api/LibraryCare/Backup/ArchiveWriter.cs",
    "src/StemStudio.Domain/Lyrics/LyricsSheet.cs",
    "src/StemStudio.Web/Components/Lyrics/LyricsPage.razor.cs",
    "src/StemStudio.Agent/Tools/Providers/LyricsToolProvider.cs",
    "src/StemStudio.Api/Lyrics/LyricsEndpoints.cs",
)


def _row(name: str) -> Component:
    (row,) = [c for c in COMPONENTS if c.name == name]
    return row


@pytest.mark.parametrize(
    ("name", "paths"),
    [("Library (022)", LIBRARY), ("Lyric assist (023)", LYRIC_ASSIST)],
)
def test_feature_rows_count_the_feature_folders(name: str, paths: tuple[str, ...]) -> None:
    row = _row(name)
    assert all(row.contains(path, test=False) for path in paths)
    assert not row.contains(paths[0], test=True)


@pytest.mark.parametrize("name", ["Library (022)", "Lyric assist (023)"])
def test_feature_rows_leave_the_neighbours_out(name: str) -> None:
    row = _row(name)
    assert not any(row.contains(path, test=False) for path in NOT_FEATURE)


def test_feature_rows_are_sub_rows_after_their_projects() -> None:
    names = [c.name for c in COMPONENTS]
    assert names.index("Agent") < names.index("Library (022)") < names.index("worker/ingest")
    assert names.index("Library (022)") < names.index("Lyric assist (023)")


# ------------------------------------------------------------------------------- owners


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        ("src/StemStudio.Api/Library/LibraryFiler.cs", "backend"),
        ("src/StemStudio.Domain/Lyrics/Assist/RhymeFinder.cs", "backend"),
        ("src/StemStudio.Web/Components/Library/FolderTile.razor", "frontend"),
        ("src/StemStudio.Web/Components/Lyrics/Assist/AssistPane.razor", "frontend"),
        ("src/StemStudio.Agent/LyricAssist/LyricAssistant.cs", "ai-agent"),
        ("src/StemStudio.Agent/Tools/Providers/LibraryToolProvider.cs", "ai-agent"),
        ("tests/fixtures/lyrics/assist/expected.json", "qa"),
    ],
)
def test_feature_paths_are_owned(path: str, owner: str) -> None:
    assert owner_of(path) == owner
