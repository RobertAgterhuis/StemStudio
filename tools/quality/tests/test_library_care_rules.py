"""Feature 013: library care rules (tasks.md T009 (a) and (b), plan.md dependency direction).

(a) Inside ``src/StemStudio.Api`` other folders may reference only
    ``StemStudio.Api.LibraryCare.Gate`` (to declare gated endpoints and skip synchroniser
    ticks); ``LibraryCare`` itself and the
    composition root ``ApiComposition.cs`` may reference every ``LibraryCare`` namespace. Fully
    qualified (``StemStudio.Api.LibraryCare.Backup``), partially qualified
    (``Api.LibraryCare.Restore``) and namespace-relative names (``LibraryCare.Storage``, which a
    ``StemStudio.Api.*`` file resolves to the API folder) count;
    ``StemStudio.Contracts.LibraryCare`` does not.
(b) No production file below ``src/`` calls the bulk ZIP helpers that extract or pack whole trees
    without per-entry checks: ``ZipFile.ExtractToDirectory``,
    ``ZipFileExtensions.ExtractToDirectory`` (also as ``archive.ExtractToDirectory``),
    ``ExtractToFile``, ``ZipFile.CreateFromDirectory``, their ``Async`` variants, and
    ``using static`` of ``ZipFile``/``ZipFileExtensions``. Hostile archives (research R-06) are
    read entry by entry through ``ArchiveReader``.
"""

from pathlib import Path

import library_care_rules
import pytest
from repo_tree import write_file

import run_all
from common import REPO_ROOT, Severity

API = "src/StemStudio.Api"


def _findings(root: Path, rel: str, text: str) -> list[tuple[str, int, str, Severity]]:
    write_file(root, rel, text)
    return [(f.message, f.line, f.owner, f.severity) for f in library_care_rules.run(root)]


def _messages(root: Path, rel: str, text: str) -> list[str]:
    return [message for message, *_rest in _findings(root, rel, text)]


# ------------------------------------------------------------------ (a) LibraryCare layering

GATE_USE = """using StemStudio.Api.LibraryCare.Gate;

namespace StemStudio.Api.Tracks;

static class TrackEndpoints
{
    static void Map(RouteGroupBuilder group) =>
        group.MapDelete("/{id}", Delete)
            .AddEndpointFilter<StemStudio.Api.LibraryCare.Gate.RequireGateOpenFilter>();
}
"""


def test_other_folders_may_use_the_gate(tmp_path: Path) -> None:
    assert _findings(tmp_path, f"{API}/Tracks/TrackEndpoints.cs", GATE_USE) == []


@pytest.mark.parametrize(
    ("text", "name"),
    [
        ("using StemStudio.Api.LibraryCare.Backup;\n", "StemStudio.Api.LibraryCare.Backup"),
        ("using static StemStudio.Api.LibraryCare.Restore.RestoreService;\n",
         "StemStudio.Api.LibraryCare.Restore.RestoreService"),
        ("using StemStudio.Api.LibraryCare;\n", "StemStudio.Api.LibraryCare"),
        ("class X { Api.LibraryCare.Storage.StorageOverviewService s; }\n",
         "Api.LibraryCare.Storage.StorageOverviewService"),
        ("class X { LibraryCare.Cleanup.CleanupPlanner p; }\n",
         "LibraryCare.Cleanup.CleanupPlanner"),
        ("class X { global::StemStudio.Api.LibraryCare.Archive.ArchiveReader r; }\n",
         "StemStudio.Api.LibraryCare.Archive.ArchiveReader"),
        ("class X { LibraryCare.Jobs.LibraryJobStore s; }\n", "LibraryCare.Jobs.LibraryJobStore"),
        ("class X { LibraryCare.LibraryCareOptions o; }\n", "LibraryCare.LibraryCareOptions"),
    ],
)  # fmt: skip
def test_other_folders_may_not_use_the_rest(tmp_path: Path, text: str, name: str) -> None:
    findings = _findings(tmp_path, f"{API}/Detail/DetailEndpoints.cs", text)

    assert findings == [
        (
            f"forbidden dependency on {name}: outside Api/LibraryCare only "
            "StemStudio.Api.LibraryCare.Gate may be referenced",
            1,
            "backend",
            Severity.ERROR,
        )
    ]


def test_library_care_and_the_composition_root_may_use_everything(tmp_path: Path) -> None:
    text = "using StemStudio.Api.LibraryCare.Backup;\nusing StemStudio.Api.LibraryCare.Restore;\n"
    write_file(tmp_path, f"{API}/LibraryCare/Restore/RestoreService.cs", text)
    write_file(tmp_path, f"{API}/LibraryCare/LibraryCareEndpoints.cs", text)

    assert _findings(tmp_path, f"{API}/ApiComposition.cs", text) == []


@pytest.mark.parametrize(
    "text",
    [
        "using StemStudio.Contracts.LibraryCare;\n",
        "class X { StemStudio.Contracts.LibraryCare.BackupOptionsDto o; }\n",
        "class X { LibraryCareLimits l; ILibraryCareClient c; }\n",
        "// see StemStudio.Api.LibraryCare.Backup\n",
        'class X { string s = "LibraryCare.Backup.BackupService"; }\n',
        "/* LibraryCare.Restore */ class X { }\n",
        "class X { int LibraryCare = 0; int y = this.LibraryCare; }\n",
    ],
)
def test_contracts_names_comments_and_strings_are_not_references(tmp_path: Path, text: str) -> None:
    assert _findings(tmp_path, f"{API}/Worker/WorkerClient.cs", text) == []


def test_projects_outside_the_api_are_left_to_the_project_rules(tmp_path: Path) -> None:
    text = "class X { LibraryCare.Backup.Thing t; }\n"

    assert _findings(tmp_path, "src/StemStudio.Web/Care/BackupsTab.razor.cs", text) == []


def test_tests_are_not_checked(tmp_path: Path) -> None:
    text = "using StemStudio.Api.LibraryCare.Backup;\n"

    assert _findings(tmp_path, "tests/StemStudio.Api.Tests/LibraryCare/BackupTests.cs", text) == []


def test_one_finding_per_reference_with_its_line(tmp_path: Path) -> None:
    body = "    LibraryCare.Backup.A a; LibraryCare.Gate.B b;\n"
    text = f"namespace StemStudio.Api.Lyrics;\n\nclass X\n{{\n{body}}}\n"

    lines = [line for _m, line, *_r in _findings(tmp_path, f"{API}/Lyrics/L.cs", text)]

    assert lines == [5]


# ------------------------------------------------------------------ (b) banned ZIP helpers


@pytest.mark.parametrize(
    ("code", "api"),
    [
        ("ZipFile.ExtractToDirectory(zip, target);", "ExtractToDirectory"),
        (
            "System.IO.Compression.ZipFile.ExtractToDirectory(zip, target, true);",
            "ExtractToDirectory",
        ),
        ("ZipFileExtensions.ExtractToDirectory(archive, target);", "ExtractToDirectory"),
        ("archive.ExtractToDirectory(target);", "ExtractToDirectory"),
        ("await ZipFile.ExtractToDirectoryAsync(zip, target, ct);", "ExtractToDirectoryAsync"),
        ("entry.ExtractToFile(path, overwrite: true);", "ExtractToFile"),
        ("await entry.ExtractToFileAsync(path, ct);", "ExtractToFileAsync"),
        ("ZipFile.CreateFromDirectory(source, zip);", "CreateFromDirectory"),
        ("await ZipFile.CreateFromDirectoryAsync(source, zip, ct);", "CreateFromDirectoryAsync"),
        ("entry ?.ExtractToFile(path);", "ExtractToFile"),
    ],
)
def test_bulk_zip_helpers_are_banned(tmp_path: Path, code: str, api: str) -> None:
    text = f"class R\n{{\n    void M() {{ {code} }}\n}}\n"

    findings = _findings(tmp_path, f"{API}/LibraryCare/Restore/StagingExtractor.cs", text)

    assert findings == [
        (
            f"banned API {api}: read and write archive entries one by one (ArchiveReader, "
            "research R-06)",
            3,
            "backend",
            Severity.ERROR,
        )
    ]


@pytest.mark.parametrize(
    "text",
    [
        "using static System.IO.Compression.ZipFile;\n",
        "using static System.IO.Compression.ZipFileExtensions;\n",
        "using Z = System.IO.Compression.ZipFile;\nclass C { void M() => Z.CreateFromDirectory(); }"
        "\n",
    ],
)
def test_using_static_of_the_zip_helpers_is_banned(tmp_path: Path, text: str) -> None:
    messages = _messages(tmp_path, "src/StemStudio.Api/LibraryCare/Archive/ArchiveWriter.cs", text)

    assert messages
    assert all(m.startswith("banned API ") for m in messages)


@pytest.mark.parametrize(
    "text",
    [
        "class C { Task ExtractAsync(Stream s) => Task.CompletedTask; }\n",
        "class C { void M() { using var zip = new ZipArchive(stream, ZipArchiveMode.Create); } }\n",
        "class C { void M() { var e = zip.CreateEntry(name); var s = e.Open(); } }\n",
        "// never call ZipFile.ExtractToDirectory here\nclass C { }\n",
        'class C { string s = "entry.ExtractToFile(path)"; }\n',
        "using System.IO.Compression;\nclass C { ZipArchive a; }\n",
    ],
)
def test_entry_by_entry_code_is_clean(tmp_path: Path, text: str) -> None:
    assert _findings(tmp_path, f"{API}/LibraryCare/Archive/ArchiveReader.cs", text) == []


def test_banned_apis_count_everywhere_in_src_but_not_in_tests(tmp_path: Path) -> None:
    text = "class C { void M() => ZipFile.CreateFromDirectory(a, b); }\n"
    write_file(tmp_path, "tests/StemStudio.Api.Tests/LibraryCare/Fixtures.cs", text)

    messages = _messages(tmp_path, "src/StemStudio.Web/Exports/Zip.cs", text)

    assert messages == [
        "banned API CreateFromDirectory: read and write archive entries one by one "
        "(ArchiveReader, research R-06)"
    ]


# ------------------------------------------------------------------ gate wiring


def test_the_repository_is_clean() -> None:
    assert library_care_rules.run(REPO_ROOT) == []


def test_main_reports_and_fails(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    write_file(tmp_path, f"{API}/Tracks/T.cs", "using StemStudio.Api.LibraryCare.Backup;\n")

    assert library_care_rules.main(["--root", str(tmp_path)]) == 1
    assert "[library-care]" in capsys.readouterr().out
    assert library_care_rules.main(["--root", str(tmp_path / "empty")]) == 0


def test_run_all_includes_the_rules() -> None:
    assert "library_care_findings" in run_all.GateResult.__dataclass_fields__
