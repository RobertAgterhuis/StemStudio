"""Feature 002: vendored MSST code is excluded from SIG; ``detail`` has a fixed dependency rule.

plan.md Complexity Tracking (accepted by CTO 2026-11-15): ``detail/vendor/**`` is third-party code
excluded from the SIG metrics like ``Migrations/``. Dependency direction: ``detail`` imports only
``ports``, ``schemas``, ``config`` and its own ``vendor``; ``jobs`` never imports ``detail``.
"""

import json
from pathlib import Path

import pytest

import dependency_check
import duplication
import sig_check
from common import Severity, is_excluded, owner_of
from dependency_check import PYTHON_ALLOWED, read_python_edges
from duplication import parse_report

PACKAGE = "worker/src/stemstudio_worker"
VENDOR = f"{PACKAGE}/detail/vendor/msst"

# One unit of 40 lines, 7 parameters and 12 branches: three hard breaches when measured.
LONG_UNIT = (
    "def demix(a, b, c, d, e, f, g):\n"
    + "".join(f"    if a == {i}:\n        b += {i}\n" for i in range(12))
    + "".join(f"    c{i} = {i}\n" for i in range(16))
    + "    return b\n"
)


def _write(root: Path, rel: str, text: str) -> Path:
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


@pytest.mark.parametrize(
    ("path", "expected"),
    [
        (f"{VENDOR}/bs_roformer.py", True),
        (f"{PACKAGE}/detail/vendor/__init__.py", True),
        (f"{PACKAGE}/detail/runtimes.py", False),
        (f"{PACKAGE}/separation/vendor_notes.py", False),
    ],
)
def test_detail_vendor_is_excluded(path: str, expected: bool) -> None:
    assert is_excluded(path) is expected


def test_sig_check_skips_vendor_but_measures_detail(tmp_path: Path) -> None:
    _write(tmp_path, f"{VENDOR}/demix.py", LONG_UNIT)
    _write(tmp_path, f"{PACKAGE}/detail/steps.py", LONG_UNIT)
    result = sig_check.run(tmp_path, [tmp_path / "worker"])
    assert result.files == 1
    assert {f.path for f in result.errors} == {f"{PACKAGE}/detail/steps.py"}


def test_jscpd_config_ignores_vendor() -> None:
    config = json.loads(duplication.CONFIG_PATH.read_text(encoding="utf-8"))
    assert "**/stemstudio_worker/detail/vendor/**" in config["ignore"]


def _entry(first: Path, second: Path) -> dict[str, object]:
    return {
        "firstFile": {"name": str(first), "start": 1, "end": 8},
        "secondFile": {"name": str(second), "start": 1, "end": 8},
        "lines": 7,
    }


def test_duplication_drops_clones_touching_vendor(tmp_path: Path) -> None:
    vendor, ours, other = (
        tmp_path / VENDOR / "mdx23c.py",
        tmp_path / PACKAGE / "detail" / "runtimes.py",
        tmp_path / PACKAGE / "detail" / "steps.py",
    )
    report = {"duplicates": [_entry(vendor, ours), _entry(ours, vendor), _entry(ours, other)]}
    clones, _ = parse_report(report, tmp_path)
    assert [(c.first, c.second) for c in clones] == [
        (f"{PACKAGE}/detail/runtimes.py", f"{PACKAGE}/detail/steps.py")
    ]


def test_detail_is_mapped_to_ports_schemas_config() -> None:
    assert PYTHON_ALLOWED["detail"] == frozenset({"ports", "schemas", "config"})


DETAIL_INIT = (
    "from stemstudio_worker.config import WorkerSettings\n"
    "from stemstudio_worker.ports import DetailAdapters\n"
    "from stemstudio_worker import schemas\n"
)


def _detail_tree(root: Path) -> None:
    _write(root, f"{PACKAGE}/detail/__init__.py", DETAIL_INIT)
    _write(root, f"{PACKAGE}/detail/runtimes.py", "from .vendor.msst import demix\n")
    _write(root, f"{PACKAGE}/detail/steps.py", "from stemstudio_worker.detail.vendor import m\n")
    # Vendored code is third party: its imports are not part of our graph.
    _write(root, f"{VENDOR}/demix.py", "from stemstudio_worker.jobs import queue\n")
    _write(root, f"{PACKAGE}/main.py", "from stemstudio_worker.detail import create\n")


def test_detail_importing_allowed_modules_and_own_vendor_is_clean(tmp_path: Path) -> None:
    _detail_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


def test_vendor_files_contribute_no_edges(tmp_path: Path) -> None:
    _detail_tree(tmp_path)
    edges = read_python_edges(tmp_path / PACKAGE, tmp_path)
    assert all("/vendor/" not in edge.path for edge in edges)


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("detail/wire.py", "from stemstudio_worker.jobs import x\n", "detail -> jobs"),
        ("detail/wire.py", "from ..separation import demucs\n", "detail -> separation"),
        ("detail/wire.py", "import stemstudio_worker.api\n", "detail -> api"),
        ("jobs/wire.py", "from stemstudio_worker.detail import create\n", "jobs -> detail"),
    ],
)
def test_forbidden_detail_edges(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _detail_tree(tmp_path)
    _write(tmp_path, f"{PACKAGE}/{rel}", text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"
    assert finding.owner == "stem-separation"


@pytest.mark.parametrize(
    "path",
    [f"{PACKAGE}/detail/steps.py", f"{VENDOR}/demix.py", "worker/tests/detail/test_steps.py"],
)
def test_detail_is_owned_by_stem_separation(path: str) -> None:
    assert owner_of(path) == "stem-separation"
