"""Feature 013: worker ``storage`` dependency rule (tasks.md T009 (c), plan.md).

``storage`` (meter, janitor, rehydration) imports only ``jobs`` (paths, stores, records),
``ports``, ``schemas`` and ``config``: never ``separation``, ``detail`` or ``api`` (nor any other
adapter). ``api`` may import ``storage`` (the storage routes); ``jobs`` and the adapters never
do; ``main.py`` composes it.
"""

from pathlib import Path

import pytest
from repo_tree import write_module

import dependency_check
from common import Severity, owner_of
from components import COMPONENTS
from dependency_check import PYTHON_ALLOWED

STORAGE_TREE = {
    "storage/__init__.py": "",
    "storage/meter.py": (
        "from stemstudio_worker.jobs.paths import DataPaths\n"
        "from stemstudio_worker.schemas import WorkerStorage\n"
        "from .walk import tree_usage\n"
    ),
    "storage/walk.py": "import os\n",
    "storage/janitor.py": (
        "from stemstudio_worker.jobs.store import JobStore\n"
        "from stemstudio_worker.ports import CacheJanitor\n"
        "from stemstudio_worker.config import WorkerSettings\n"
    ),
    "jobs/paths.py": "from stemstudio_worker.schemas import StemName\n",
    "jobs/store.py": "from .paths import DataPaths\n",
    "api/storage.py": (
        "from stemstudio_worker.storage.services import StorageServices\n"
        "from stemstudio_worker.jobs.store import JobStore\n"
    ),
    "main.py": "from stemstudio_worker.storage.services import create_storage_services\n",
}


def _tree(root: Path) -> None:
    for rel, text in STORAGE_TREE.items():
        write_module(root, rel, text)


def test_storage_is_mapped_to_jobs_ports_schemas_config() -> None:
    assert PYTHON_ALLOWED["storage"] == frozenset({"jobs", "ports", "schemas", "config"})
    assert "storage" in (PYTHON_ALLOWED["api"] or frozenset())


def test_the_storage_tree_is_clean(tmp_path: Path) -> None:
    _tree(tmp_path)

    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("storage/meter.py", "from ..separation import demucs\n", "storage -> separation"),
        ("storage/janitor.py", "from ..detail.registry import MODELS\n", "storage -> detail"),
        ("storage/rehydrate.py", "import stemstudio_worker.api.problems\n", "storage -> api"),
        ("storage/rehydrate.py", "from ..render import reader\n", "storage -> render"),
        ("storage/rehydrate.py", "from ..devices import runner\n", "storage -> devices"),
        ("jobs/runtime.py", "from stemstudio_worker.storage import meter\n", "jobs -> storage"),
        ("separation/fake.py", "from ..storage.walk import tree_usage\n", "separation -> storage"),
        ("detail/steps.py", "from stemstudio_worker import storage\n", "detail -> storage"),
    ],
)  # fmt: skip
def test_forbidden_storage_edges(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _tree(tmp_path)
    write_module(tmp_path, rel, text)

    findings = [f for f in dependency_check.run(tmp_path) if f.severity is Severity.ERROR]

    assert [f.message for f in findings if "cycle" not in f.message] == [
        f"forbidden dependency {message}"
    ]


def test_storage_code_is_owned_and_measured_as_its_own_component() -> None:
    assert owner_of("worker/src/stemstudio_worker/storage/janitor.py") == "stem-separation"
    assert owner_of("worker/tests/storage/test_janitor.py") == "stem-separation"
    (component,) = [c for c in COMPONENTS if c.name == "worker/storage"]
    assert component.contains("worker/src/stemstudio_worker/storage/meter.py", test=False)
