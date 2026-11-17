import pytest

import reference_validator as rv
from backup_library import TRACK_ID, ArchiveRecipe, package
from backup_zip import DEFLATED, RawEntry, build_zip


def _base() -> rv._Archive:
    archive = rv._Archive(package(ArchiveRecipe("unit.zip")).build())
    rv.step2_open(archive)
    return archive


@pytest.mark.parametrize(
    "path",
    [
        f"tracks/{TRACK_ID}/stems/vocals.flac",
        f"tracks/{TRACK_ID}/artwork/user/cover.jpg",
        f"tracks/{TRACK_ID}/detail/drums/kick.flac",
        "uploads/" + "a" * 64 + ".mp3",
        f"assets/{TRACK_ID}/audio.flac",
        f"generations/{TRACK_ID}/takes/take-1.flac",
        f"voices/{TRACK_ID}/sample.flac",
    ],
)
def test_layout_allows_the_generated_paths(path: str) -> None:
    assert rv.is_allowed(path)


@pytest.mark.parametrize(
    "path",
    [
        f"tracks/{TRACK_ID}/stems/kazoo.flac",
        f"tracks/{TRACK_ID}/detail/bass/kick.flac",
        f"tracks/{TRACK_ID.upper()}/stems/vocals.flac",
        f"assets/{TRACK_ID}/..",
        f"assets/{TRACK_ID}/a..b",
        f"generations/{TRACK_ID}/a/b/c/d",
        "../../x",
        "library-care/jobs/x.json",
    ],
)
def test_layout_refuses_other_paths(path: str) -> None:
    assert not rv.is_allowed(path)


def test_manifest_that_is_not_json_is_invalid() -> None:
    data = build_zip([RawEntry(b"manifest.json", b"{not json", DEFLATED)])
    assert rv.validate(data) == rv.Outcome("archive-invalid", 2)


def test_too_many_entries_is_invalid(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(rv, "MAX_ENTRIES", 3)
    assert rv.validate(package(ArchiveRecipe("unit.zip")).build()) == rv.Outcome(
        "archive-invalid", 2
    )


def test_size_differing_from_the_manifest_is_corrupt() -> None:
    archive = _base()
    archive.manifest["entries"][1]["sizeBytes"] += 1
    with pytest.raises(rv.ArchiveProblemError) as problem:
        rv.step3_entries(archive)
    assert (problem.value.code, problem.value.step) == ("archive-corrupt", 3)


def test_space_check_uses_the_margin() -> None:
    archive = _base()
    total = archive.manifest["totals"]["bytes"]
    rv.step5_space(archive, total + rv.SPACE_MARGIN)
    with pytest.raises(rv.ArchiveProblemError, match="insufficient-disk-space"):
        rv.step5_space(archive, total + rv.SPACE_MARGIN - 1)


def test_history_differing_from_the_manifest_is_corrupt() -> None:
    archive = _base()
    rv.step3_entries(archive)
    rv.step6_extract(archive)
    archive.manifest["schema"]["migrations"] = archive.manifest["schema"]["migrations"][:-1]
    with pytest.raises(rv.ArchiveProblemError, match="history differs"):
        rv.step7_database(archive)


def test_problem_message_names_code_and_step() -> None:
    assert str(rv.ArchiveProblemError("archive-unsafe", 3, "x")) == "archive-unsafe (step 3): x"
