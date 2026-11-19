"""Feature 009: the generator in the gate (plan.md dependency direction, tasks 009:T007).

Generator (imports inside ``stemstudio_generator``): ``api -> jobs -> ports <- {engines, devices,
audio}``; ``engines``, ``devices`` and ``audio`` import only ``ports``, ``schemas`` and
``config``; nothing but ``main.py`` imports an adapter (only the composition root composes);
``ports``, ``schemas`` and ``config`` are leaves. The worker and the generator never import each
other (they share only the GPU lease file).

The generator is measured like the worker: it is a default root of ``sig_check`` and
``duplication``, its ``src/`` is production code, it has component rows, owners (``generative``
for the service code and the tests, the architect for the Phase 0 skeleton), tests and a coverage
report. It has no vendored code. A clone between worker and generator production
code is a warning (the services may not share code), every other production clone an error.

.NET: ``Api/Generation`` and the generator client ``Api/Generator`` never reference
``StemStudio.Web``.
"""

from collections.abc import Callable
from pathlib import Path

import pytest
from repo_tree import GENERATOR_PACKAGE as GEN
from repo_tree import WORKER_PACKAGE, api_findings, write_generator_tree
from repo_tree import write_file as _write
from repo_tree import write_generator_module as _gen
from repo_tree import write_module as _worker

import dependency_check
import duplication
import sig_check
from common import (
    DEFAULT_ROOTS,
    Severity,
    default_paths,
    is_excluded,
    is_production,
    owner_of,
)
from components import COMPONENTS
from dependency_check import DOTNET_FOLDER_FORBIDDEN, GENERATOR_ALLOWED
from duplication import Clone

LEAVES = frozenset({"schemas", "config"})
ADAPTER = LEAVES | {"ports"}

GENERATOR_TREE = {
    "__init__.py": '"""Generator."""\n',
    "schemas.py": "from pydantic import BaseModel\n",
    "config.py": "import os\nfrom stemstudio_generator.schemas import GenerationKind\n",
    "ports.py": "import numpy as np\nfrom stemstudio_generator.schemas import TakeLabel\n",
    "engines/__init__.py": "from .fake import FakeMusicEngine\n",
    "engines/fake.py": (
        "from stemstudio_generator.ports import MusicEngine, RestyleJob\n"
        "from ..schemas import GenerationKind\n"
        "from .. import config\n"
    ),
    "engines/acestep_engine.py": (
        "def load() -> object:\n    import acestep.handler\n\n    return acestep.handler\n"
    ),
    "devices/probe.py": "from stemstudio_generator.ports import DeviceProbe\n",
    "devices/lease.py": "import fcntl\nfrom ..ports import GpuGate\nfrom ..config import X\n",
    "audio/summing.py": "import numpy as np\nfrom ..ports import AudioBuffer\n",
    "jobs/scheduler.py": "from stemstudio_generator.ports import JobStore, MusicEngine\n",
    "jobs/store.py": "from ..schemas import Generation\nfrom . import scheduler\n",
    "api/routes.py": "from fastapi import APIRouter\nfrom ..jobs import scheduler\n",
    "api/problems.py": "from stemstudio_generator import schemas\n",
    "main.py": (
        "from stemstudio_generator.api import routes\n"
        "from stemstudio_generator.jobs.scheduler import Scheduler\n"
        "from stemstudio_generator.engines import FakeMusicEngine\n"
        "from stemstudio_generator.devices.probe import TorchProbe\n"
        "from stemstudio_generator.audio import summing\n"
    ),
}


# ------------------------------------------------------------------ generator layer rule


def test_generator_layer_map() -> None:
    assert {
        "__init__": LEAVES,
        "schemas": LEAVES,
        "config": LEAVES,
        "ports": LEAVES,
        "engines": ADAPTER,
        "devices": ADAPTER,
        "audio": ADAPTER,
        "jobs": ADAPTER,
        "api": ADAPTER | {"jobs"},
        "main": None,
    } == GENERATOR_ALLOWED


def test_generator_importing_allowed_modules_is_clean(tmp_path: Path) -> None:
    write_generator_tree(tmp_path, GENERATOR_TREE)
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "edge"),
    [
        (
            "engines/wire.py",
            "from stemstudio_generator.devices import probe\n",
            "engines -> devices",
        ),
        ("engines/wire.py", "from ..jobs import scheduler\n", "engines -> jobs"),
        ("engines/wire.py", "from ..audio.summing import mix\n", "engines -> audio"),
        ("engines/wire.py", "def f() -> None:\n    from ..api import routes\n", "engines -> api"),
        ("devices/wire.py", "import stemstudio_generator.engines.fake\n", "devices -> engines"),
        ("audio/wire.py", "from stemstudio_generator import jobs\n", "audio -> jobs"),
        ("jobs/wire.py", "from ..engines import FakeMusicEngine\n", "jobs -> engines"),
        ("jobs/wire.py", "from ..audio import summing\n", "jobs -> audio"),
        ("jobs/wire.py", "from ..api import routes\n", "jobs -> api"),
        ("api/wire.py", "from ..devices.probe import TorchProbe\n", "api -> devices"),
        ("api/wire.py", "from .. import main\n", "api -> main"),
        ("ports.py", "from stemstudio_generator.jobs import store\n", "ports -> jobs"),
        ("schemas.py", "from stemstudio_generator.ports import Clock\n", "schemas -> ports"),
        ("config.py", "from .engines import fake\n", "config -> engines"),
    ],
)
def test_generator_forbidden_edges(tmp_path: Path, rel: str, text: str, edge: str) -> None:
    write_generator_tree(tmp_path, GENERATOR_TREE)
    _gen(tmp_path, rel, text)
    findings = dependency_check.run(tmp_path)
    (finding,) = [f for f in findings if not f.message.startswith("cycle: ")]
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {edge}"
    assert finding.path == f"{GEN}/{rel}"
    assert all(f.severity is Severity.ERROR for f in findings)


def test_generator_module_missing_from_the_map_is_a_warning(tmp_path: Path) -> None:
    write_generator_tree(tmp_path, GENERATOR_TREE)
    _gen(tmp_path, "voices/store.py", "from ..ports import Clock\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.WARNING
    assert finding.message == "voices is not in the dependency map"


def test_generator_cycle_is_an_error(tmp_path: Path) -> None:
    write_generator_tree(tmp_path, GENERATOR_TREE)
    _gen(tmp_path, "schemas.py", "from stemstudio_generator.config import X\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == "cycle: config -> schemas -> config"


def test_generator_finding_names_the_generative_owner(tmp_path: Path) -> None:
    write_generator_tree(tmp_path, GENERATOR_TREE)
    _gen(tmp_path, "engines/wire.py", "\n\nfrom ..jobs import x\n")
    (finding,) = dependency_check.run(tmp_path)
    assert (finding.line, finding.owner) == (3, "generative")


WRITERS: dict[str, Callable[[Path, str, str], Path]] = {"worker": _worker, "generator": _gen}


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        (
            "worker:jobs/wire.py",
            "import stemstudio_generator.ports\n",
            "jobs -> stemstudio_generator",
        ),
        (
            "worker:devices/lease.py",
            "def f() -> None:\n    from stemstudio_generator.config import X\n",
            "devices -> stemstudio_generator",
        ),
        (
            "generator:devices/wire.py",
            "from stemstudio_worker.devices import lease\n",
            "devices -> stemstudio_worker",
        ),
        ("generator:main.py", "import stemstudio_worker\n", "main -> stemstudio_worker"),
    ],
)
def test_worker_and_generator_never_import_each_other(
    tmp_path: Path, rel: str, text: str, message: str
) -> None:
    package, rel = rel.split(":")
    WRITERS[package](tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"
    assert finding.path.endswith(rel)


def test_worker_package_rules_ignore_the_generator(tmp_path: Path) -> None:
    _worker(tmp_path, "jobs/runner.py", "from stemstudio_worker.ports import JobStore\n")
    write_generator_tree(tmp_path, GENERATOR_TREE)
    assert dependency_check.run(tmp_path) == []
    assert (tmp_path / WORKER_PACKAGE).is_dir()


# ------------------------------------------------------------------ measured like the worker


def test_generator_is_a_default_root(tmp_path: Path) -> None:
    assert "generator" in DEFAULT_ROOTS
    (tmp_path / "generator").mkdir()
    assert default_paths(tmp_path) == [tmp_path / "generator"]


@pytest.mark.parametrize(
    ("path", "expected"),
    [
        (f"{GEN}/engines/fake.py", True),
        (f"{GEN}/main.py", True),
        (f"{GEN}/schemas.py", True),
        ("generator/tests/test_config.py", False),
        ("generator/tests/contract/docs.py", False),
    ],
)
def test_generator_src_is_production_code(path: str, expected: bool) -> None:
    assert is_production(path) is expected


LONG_UNIT = (
    "def long() -> int:\n" + "".join(f"    x{i} = {i}\n" for i in range(31)) + "    return 0\n"
)


def test_sig_check_measures_the_generator_by_default(tmp_path: Path) -> None:
    _gen(tmp_path, "engines/long.py", LONG_UNIT)
    result = sig_check.run(tmp_path, [])
    (finding,) = result.findings
    assert (finding.check, finding.severity) == ("unit-size", Severity.ERROR)
    assert (finding.path, finding.owner) == (f"{GEN}/engines/long.py", "generative")


def test_generator_clone_is_a_production_clone() -> None:
    clone = Clone(f"{GEN}/engines/fake.py", 1, "generator/tests/test_x.py", 1, 6)
    assert clone.in_production
    assert not clone.cross_service
    (finding,) = duplication.evaluate([clone])
    assert finding.severity is Severity.ERROR


CROSS_SERVICE = "(cross-service: worker and generator may not share code)"


@pytest.mark.parametrize(
    ("first", "second"),
    [
        (f"{GEN}/config.py", f"{WORKER_PACKAGE}/config.py"),
        (f"{WORKER_PACKAGE}/schemas.py", f"{GEN}/schemas.py"),
    ],
)
def test_worker_generator_clone_is_a_cross_service_warning(first: str, second: str) -> None:
    clone = Clone(first, 104, second, 165, 13)
    assert clone.in_production
    assert clone.cross_service
    (finding,) = duplication.evaluate([clone])
    assert finding.severity is Severity.WARNING
    assert finding.message == f"13 duplicated lines, also in {second}:165 {CROSS_SERVICE}"


@pytest.mark.parametrize(
    ("first", "second"),
    [
        (f"{GEN}/config.py", f"{GEN}/ports.py"),
        (f"{WORKER_PACKAGE}/config.py", "src/StemStudio.Api/Program.cs"),
        (f"{GEN}/config.py", "worker/tests/test_config.py"),
    ],
)
def test_other_production_clones_stay_errors(first: str, second: str) -> None:
    clone = Clone(first, 1, second, 1, 6)
    assert not clone.cross_service
    (finding,) = duplication.evaluate([clone])
    assert finding.severity is Severity.ERROR
    assert "cross-service" not in finding.message


@pytest.mark.parametrize(
    "path",
    [
        f"{GEN}/engines/acestep_engine.py",
        f"{GEN}/engines/fake.py",
        f"{GEN}/devices/lease.py",
        "generator/tests/engines/test_fake.py",
    ],
)
def test_generator_has_no_vendored_code(path: str) -> None:
    assert not is_excluded(path)


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        (f"{GEN}/api/routes.py", "generative"),
        (f"{GEN}/jobs/scheduler.py", "generative"),
        (f"{GEN}/devices/probe.py", "generative"),
        (f"{GEN}/engines/acestep_engine.py", "generative"),
        (f"{GEN}/audio/summing.py", "generative"),
        (f"{GEN}/main.py", "generative"),
        ("generator/tests/test_config.py", "generative"),
        ("generator/tests/contract/test_generator_contract.py", "generative"),
        (f"{GEN}/ports.py", "architect"),
        (f"{GEN}/schemas.py", "architect"),
        (f"{GEN}/config.py", "architect"),
        (f"{GEN}/__init__.py", "architect"),
    ],
)
def test_generator_owners(path: str, owner: str) -> None:
    assert owner_of(path) == owner


@pytest.mark.parametrize(
    ("name", "inside", "outside"),
    [
        ("generator/api", "api/routes.py", "jobs/scheduler.py"),
        ("generator/jobs", "jobs/scheduler.py", "api/routes.py"),
        ("generator/devices", "devices/probe.py", "engines/fake.py"),
        ("generator/engines", "engines/fake.py", "devices/probe.py"),
        ("generator/audio", "audio/summing.py", "engines/fake.py"),
        ("generator/core", "ports.py", "jobs/store.py"),
    ],
)
def test_generator_components_measure_their_production_code(
    name: str, inside: str, outside: str
) -> None:
    (component,) = [c for c in COMPONENTS if c.name == name]
    assert component.contains(f"{GEN}/{inside}", test=False)
    assert not component.contains(f"{GEN}/{outside}", test=False)
    assert not component.contains(f"generator/tests/{inside}", test=True)
    assert not component.contains(f"worker/src/stemstudio_worker/{inside}", test=False)


@pytest.mark.parametrize("name", ["__init__.py", "config.py", "main.py", "ports.py", "schemas.py"])
def test_generator_core_component_holds_the_top_level_modules(name: str) -> None:
    (core,) = [c for c in COMPONENTS if c.name == "generator/core"]
    assert core.contains(f"{GEN}/{name}", test=False)


# ------------------------------------------------------------------ .NET feature folders

API = "src/StemStudio.Api"


@pytest.mark.parametrize("folder", ["StemStudio.Api/Generation", "StemStudio.Api/Generator"])
def test_generation_folders_forbid_web(folder: str) -> None:
    assert "StemStudio.Web" in DOTNET_FOLDER_FORBIDDEN[folder]


@pytest.mark.parametrize(
    ("rel", "text", "line"),
    [
        ("Generation/RestyleService.cs", "using System;\nusing StemStudio.Web.Pages;\n", 2),
        ("Generation/Sync/GenerationSyncService.cs", "var p = StemStudio.Web.Foo.Bar;\n", 1),
        ("Generator/GeneratorClient.cs", "using static StemStudio.Web.Text;\n", 1),
    ],
)
def test_generation_folder_referencing_web_is_forbidden(
    tmp_path: Path, rel: str, text: str, line: int
) -> None:
    edge = f"StemStudio.Api/{rel.split('/', maxsplit=1)[0]} -> StemStudio.Web"
    expected = (Severity.ERROR, f"forbidden dependency {edge}", line, "backend")
    assert api_findings(tmp_path, rel, text) == [expected]


ALLOWED_GENERATION = """namespace StemStudio.Api.Generation;

using StemStudio.Contracts.Generation;
using StemStudio.Domain.Generation;
using StemStudio.Domain.Styles;
using StemStudio.Persistence;
using StemStudio.Api.Generator;
using StemStudio.Api.Assets;

/// <summary>Never calls StemStudio.Web; the UI polls the API.</summary>
public sealed class RestyleService
{
    private const string Route = "StemStudio.Web/generations";
}
"""


def test_allowed_generation_references_are_clean(tmp_path: Path) -> None:
    _write(tmp_path, f"{API}/Generation/RestyleService.cs", ALLOWED_GENERATION)
    _write(tmp_path, f"{API}/Generator/GeneratorClient.cs", "using StemStudio.Contracts;\n")
    assert dependency_check.run(tmp_path) == []
