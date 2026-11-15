from pathlib import Path

import pytest

import dependency_check
from common import REPO_ROOT, Severity
from dependency_check import (
    DOTNET_ALLOWED,
    PYTHON_ALLOWED,
    Edge,
    check_edges,
    find_cycles,
    read_dotnet_edges,
    read_python_edges,
)

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
{refs}
  </ItemGroup>
</Project>
"""


def _project(root: Path, name: str, *refs: str) -> None:
    folder = root / "src" / name
    folder.mkdir(parents=True)
    lines = "\n".join(f'    <ProjectReference Include="..\\{ref}\\{ref}.csproj" />' for ref in refs)
    (folder / f"{name}.csproj").write_text(CSPROJ.format(refs=lines), encoding="utf-8")


def _module(root: Path, rel: str, text: str) -> None:
    path = root / "worker" / "src" / "stemstudio_worker" / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _skeleton(root: Path) -> None:
    _dotnet_skeleton(root)
    _python_skeleton(root)


def _dotnet_skeleton(root: Path) -> None:
    _project(root, "StemStudio.Domain")
    _project(root, "StemStudio.Contracts")
    _project(root, "StemStudio.Persistence", "StemStudio.Domain")
    _project(
        root,
        "StemStudio.Api",
        "StemStudio.Domain",
        "StemStudio.Persistence",
        "StemStudio.Contracts",
    )


def _python_skeleton(root: Path) -> None:
    _module(root, "__init__.py", '"""Package."""\n')
    _module(root, "schemas.py", "import json\nfrom pydantic import BaseModel\n")
    _module(root, "ports.py", "from stemstudio_worker.schemas import X\n")
    _module(root, "ingest/__init__.py", "from stemstudio_worker.ports import IngestAdapters\n")
    _module(root, "jobs/runner.py", "from ..ports import Separator\nfrom . import helpers\n")
    _module(root, "api/routes.py", "from stemstudio_worker import jobs\n")
    _module(root, "main.py", "from .ingest import create\nfrom . import api\n")


def test_skeleton_graph_is_clean(tmp_path: Path) -> None:
    _skeleton(tmp_path)
    _project(tmp_path, "StemStudio.Web", "StemStudio.Contracts")
    assert dependency_check.run(tmp_path) == []


def test_forbidden_web_to_persistence(tmp_path: Path) -> None:
    _skeleton(tmp_path)
    _project(tmp_path, "StemStudio.Web", "StemStudio.Contracts", "StemStudio.Persistence")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == "forbidden dependency StemStudio.Web -> StemStudio.Persistence"
    assert finding.path == "src/StemStudio.Web/StemStudio.Web.csproj"
    assert finding.owner == "frontend"


def test_forbidden_ingest_to_separation(tmp_path: Path) -> None:
    _skeleton(tmp_path)
    _module(tmp_path, "ingest/fetch.py", "import os\nimport stemstudio_worker.separation.demucs\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.message == "forbidden dependency ingest -> separation"
    assert (finding.path, finding.line) == ("worker/src/stemstudio_worker/ingest/fetch.py", 2)
    assert finding.owner == "audio-ingest"


def test_jobs_may_not_import_adapters(tmp_path: Path) -> None:
    _skeleton(tmp_path)
    _module(tmp_path, "jobs/wire.py", "from stemstudio_worker.analysis import create\n")
    messages = [f.message for f in dependency_check.run(tmp_path)]
    assert messages == ["forbidden dependency jobs -> analysis"]


def test_relative_import_resolution(tmp_path: Path) -> None:
    _module(tmp_path, "jobs/a.py", "from ..ingest import x\nfrom .b import y\nfrom ... import z\n")
    package = tmp_path / "worker" / "src" / "stemstudio_worker"
    edges = read_python_edges(package, tmp_path)
    assert [(e.source, e.target, e.line) for e in edges] == [
        ("jobs", "ingest", 1),
        ("jobs", "z", 3),
    ]


def test_cycle_detection_reports_each_cycle_once() -> None:
    edges = [
        Edge("b", "c", "p", 0),
        Edge("c", "b", "p", 0),
        Edge("a", "b", "p", 0),
        Edge("c", "a", "p", 0),
        Edge("d", "d", "p", 0),
    ]
    assert find_cycles(edges) == [["a", "b", "c", "a"], ["b", "c", "b"], ["d", "d"]]


def test_cycles_and_unmapped_modules() -> None:
    edges = [Edge("Foo", "Bar", "src/Foo/Foo.csproj", 0), Edge("Bar", "Foo", "x", 0)]
    findings = check_edges(edges, DOTNET_ALLOWED)
    summary = sorted((f.severity, f.message) for f in findings)
    assert summary == [
        (Severity.ERROR, "cycle: Bar -> Foo -> Bar"),
        (Severity.WARNING, "Bar is not in the dependency map"),
        (Severity.WARNING, "Foo is not in the dependency map"),
    ]


def test_main_is_unrestricted() -> None:
    assert PYTHON_ALLOWED["main"] is None
    assert check_edges([Edge("main", "jobs", "m", 1)], PYTHON_ALLOWED) == []


def test_read_dotnet_edges_of_real_repo() -> None:
    edges = {(e.source, e.target) for e in read_dotnet_edges(REPO_ROOT / "src", REPO_ROOT)}
    assert ("StemStudio.Web", "StemStudio.Contracts") in edges


def test_run_without_worker(tmp_path: Path) -> None:
    _project(tmp_path, "StemStudio.Domain", "StemStudio.Api")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.message == "forbidden dependency StemStudio.Domain -> StemStudio.Api"


@pytest.mark.parametrize(("extra", "code"), [((), 0), (("StemStudio.Persistence",), 1)])
def test_main_exit_code(
    tmp_path: Path, capsys: pytest.CaptureFixture[str], extra: tuple[str, ...], code: int
) -> None:
    _skeleton(tmp_path)
    _project(tmp_path, "StemStudio.Web", "StemStudio.Contracts", *extra)
    assert dependency_check.main(["--root", str(tmp_path)]) == code
    assert f"dependency_check: {code} errors" in capsys.readouterr().out
