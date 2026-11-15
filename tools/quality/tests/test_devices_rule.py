"""Feature 003: dependency rule for the worker ``devices`` package (plan.md dependency direction).

``devices`` imports only ``ports``, ``schemas`` and ``config``. ``separation``, ``detail``,
``jobs`` and ``api`` receive the ``DeviceRunner`` from the composition root and never import
``devices``; only ``main.py`` composes it.
"""

from pathlib import Path

import pytest

import dependency_check
from common import Severity, owner_of
from dependency_check import PYTHON_ALLOWED

PACKAGE = "worker/src/stemstudio_worker"

DEVICES_INIT = (
    "from stemstudio_worker.config import WorkerSettings\n"
    "from stemstudio_worker.ports import DeviceRunner, GpuNeed\n"
    "from stemstudio_worker import schemas\n"
    "from .probe import probe\n"
)


def _write(root: Path, rel: str, text: str) -> None:
    path = root / PACKAGE / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _devices_tree(root: Path) -> None:
    _write(root, "devices/__init__.py", DEVICES_INIT)
    _write(root, "devices/probe.py", "from ..schemas import DeviceInfo\nfrom . import nvml\n")
    _write(root, "devices/runner.py", "from stemstudio_worker.ports import DeviceUse\n")
    _write(root, "main.py", "from stemstudio_worker.devices import create_device_runner\n")


def test_devices_is_mapped_to_ports_schemas_config() -> None:
    assert PYTHON_ALLOWED["devices"] == frozenset({"ports", "schemas", "config"})


def test_devices_importing_allowed_modules_is_clean(tmp_path: Path) -> None:
    _devices_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("devices/wire.py", "from ..separation import x\n", "devices -> separation"),
        ("devices/wire.py", "from ..detail import loaders\n", "devices -> detail"),
        ("devices/wire.py", "import stemstudio_worker.jobs.runner\n", "devices -> jobs"),
        ("devices/wire.py", "from stemstudio_worker import api\n", "devices -> api"),
        ("devices/wire.py", "from stemstudio_worker import main\n", "devices -> main"),
        ("separation/demucs.py", "from ..devices import runner\n", "separation -> devices"),
        ("detail/steps.py", "from stemstudio_worker.devices import lease\n", "detail -> devices"),
        ("jobs/runner.py", "import stemstudio_worker.devices\n", "jobs -> devices"),
        ("api/routes.py", "from stemstudio_worker import devices\n", "api -> devices"),
        ("ingest/fetch.py", "from stemstudio_worker.devices import x\n", "ingest -> devices"),
        ("analysis/beats.py", "from ..devices.probe import probe\n", "analysis -> devices"),
    ],
)
def test_forbidden_devices_edges(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _devices_tree(tmp_path)
    _write(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"


@pytest.mark.parametrize(
    "path",
    [f"{PACKAGE}/devices/runner.py", "worker/tests/devices/test_runner.py"],
)
def test_devices_is_owned_by_stem_separation(path: str) -> None:
    assert owner_of(path) == "stem-separation"


def test_devices_finding_names_its_owner(tmp_path: Path) -> None:
    _devices_tree(tmp_path)
    _write(tmp_path, "devices/wire.py", "from stemstudio_worker.jobs import x\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.owner == "stem-separation"
