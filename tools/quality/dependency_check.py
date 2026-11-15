"""Dependency-direction check for the .NET projects and the Python worker (plan.md).

.NET (``<ProjectReference>`` in ``src/*/*.csproj``)::

    Web -> Contracts;  Api -> {Contracts, Domain, Persistence};  Persistence -> Domain
    Domain and Contracts reference no StemStudio project.

Worker (imports inside ``stemstudio_worker``)::

    api -> jobs -> ports <- {ingest, separation, analysis, detail, devices};
    everything -> schemas, config
    detail may also import its own vendor package (detail/vendor/** is third-party code and
    contributes no edges); no module imports devices (feature 003: separation and detail get
    the DeviceRunner injected); main.py is the composition root and may import every module.

Forbidden edges and cycles fail the gate; modules missing from the map are warnings.

Usage::

    python tools/quality/dependency_check.py [--root DIR]

Exit code 0 = clean, 1 = forbidden edge or cycle.
"""

import ast
import sys

# Project files are trusted repository content; Python 3.12's expat does not expand external
# entities and guards against entity-expansion attacks, so defusedxml is not needed.
import xml.etree.ElementTree as ET
from collections.abc import Iterator, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path, PureWindowsPath

from common import (
    Finding,
    Severity,
    build_parser,
    count_errors,
    format_findings,
    is_excluded,
    relative,
)

DOTNET_ALLOWED: Mapping[str, frozenset[str]] = {
    "StemStudio.Domain": frozenset(),
    "StemStudio.Contracts": frozenset(),
    "StemStudio.Persistence": frozenset({"StemStudio.Domain"}),
    "StemStudio.Api": frozenset(
        {"StemStudio.Domain", "StemStudio.Persistence", "StemStudio.Contracts"}
    ),
    "StemStudio.Web": frozenset({"StemStudio.Contracts"}),
}
"""Allowed project references per production project."""

_LEAVES = frozenset({"schemas", "config"})
_ADAPTER = _LEAVES | {"ports"}

PYTHON_ALLOWED: Mapping[str, frozenset[str] | None] = {
    "__init__": _LEAVES,
    "schemas": _LEAVES,
    "config": _LEAVES,
    "ports": _LEAVES,
    "ingest": _ADAPTER,
    "separation": _ADAPTER,
    "analysis": _ADAPTER,
    "detail": _ADAPTER,
    "devices": _ADAPTER,
    "jobs": _ADAPTER,
    "api": _ADAPTER | {"jobs"},
    "main": None,
}
"""Allowed imported top-level modules per worker module; ``None`` = unrestricted."""

PYTHON_PACKAGE = "stemstudio_worker"
"""Name of the worker package."""


@dataclass(frozen=True, slots=True)
class Edge:
    """A dependency ``source -> target`` with its location."""

    source: str
    target: str
    path: str
    line: int


def read_dotnet_edges(src_dir: Path, root: Path) -> list[Edge]:
    """Return the ``ProjectReference`` edges of every ``*.csproj`` directly below ``src_dir``."""
    edges: list[Edge] = []
    for project in sorted(src_dir.glob("*/*.csproj")):
        tree = ET.parse(project)  # noqa: S314  (trusted repository files)
        for reference in tree.iter("ProjectReference"):
            target = PureWindowsPath(reference.get("Include", "")).stem
            edges.append(Edge(project.stem, target, relative(project, root), 0))
    return edges


def read_python_edges(package_dir: Path, root: Path) -> list[Edge]:
    """Return the internal import edges between top-level modules of the worker package.

    Excluded files (vendored third-party code) contribute no edges.
    """
    edges: list[Edge] = []
    for module_file in sorted(package_dir.rglob("*.py")):
        rel_path = relative(module_file, root)
        if is_excluded(rel_path):
            continue
        source = _top_module(module_file.relative_to(package_dir))
        tree = ast.parse(module_file.read_text(encoding="utf-8"))
        edges.extend(
            Edge(source, target, rel_path, line)
            for target, line in _imports(tree, module_file.relative_to(package_dir))
            if target != source
        )
    return edges


def _top_module(rel: Path) -> str:
    """``jobs/runner.py`` -> ``jobs``, ``main.py`` -> ``main``, ``__init__.py`` -> ``__init__``."""
    return rel.parts[0].removesuffix(".py")


def _imports(tree: ast.Module, rel: Path) -> Iterator[tuple[str, int]]:
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            for alias in node.names:
                yield from _absolute_target(alias.name, node.lineno)
        elif isinstance(node, ast.ImportFrom):
            yield from _from_targets(node, rel)


def _absolute_target(dotted: str, line: int) -> Iterator[tuple[str, int]]:
    parts = dotted.split(".")
    if parts[0] == PYTHON_PACKAGE and len(parts) > 1:
        yield parts[1], line


def _from_targets(node: ast.ImportFrom, rel: Path) -> Iterator[tuple[str, int]]:
    """Targets of ``from X import Y``; ``from <package> import y`` / ``from . import y`` -> y."""
    dotted = _resolve_from(node, rel)
    if dotted in ([], [PYTHON_PACKAGE]):
        yield from ((alias.name, node.lineno) for alias in node.names)
    elif dotted[0] == PYTHON_PACKAGE:
        yield dotted[1], node.lineno
    elif node.level > 0:
        yield dotted[0], node.lineno


def _resolve_from(node: ast.ImportFrom, rel: Path) -> list[str]:
    """Absolute module parts of an ``ImportFrom`` (relative ones relative to the package)."""
    module = node.module.split(".") if node.module else []
    if node.level == 0:
        return module
    parents = list(rel.parent.parts)
    return [*parents[: max(len(parents) - node.level + 1, 0)], *module]


def check_edges(
    edges: Sequence[Edge], allowed: Mapping[str, frozenset[str] | None]
) -> list[Finding]:
    """Return findings for forbidden edges, unmapped modules and cycles."""
    findings = [f for f in (_edge_finding(edge, allowed) for edge in edges) if f is not None]
    return [*findings, *_unmapped(edges, allowed), *_cycles(edges)]


def _unmapped(edges: Sequence[Edge], allowed: Mapping[str, object]) -> list[Finding]:
    nodes = sorted({edge.source for edge in edges} - set(allowed))
    return [
        Finding("dependency", Severity.WARNING, node, 0, f"{node} is not in the dependency map")
        for node in nodes
    ]


def _cycles(edges: Sequence[Edge]) -> list[Finding]:
    return [
        Finding("dependency", Severity.ERROR, cycle[0], 0, "cycle: " + " -> ".join(cycle))
        for cycle in find_cycles(edges)
    ]


def _edge_finding(edge: Edge, allowed: Mapping[str, frozenset[str] | None]) -> Finding | None:
    if edge.source not in allowed:
        return None
    permitted = allowed[edge.source]
    if permitted is None or edge.target in permitted:
        return None
    message = f"forbidden dependency {edge.source} -> {edge.target}"
    return Finding("dependency", Severity.ERROR, edge.path, edge.line, message)


def find_cycles(edges: Sequence[Edge]) -> list[list[str]]:
    """Return each elementary cycle once, as ``[a, b, ..., a]`` starting at its smallest node."""
    graph: dict[str, set[str]] = {}
    for edge in edges:
        graph.setdefault(edge.source, set()).add(edge.target)
    cycles: set[tuple[str, ...]] = set()
    for start in sorted(graph):
        _walk(graph, [start], cycles)
    return [[*cycle, cycle[0]] for cycle in sorted(cycles)]


def _walk(graph: Mapping[str, set[str]], path: list[str], cycles: set[tuple[str, ...]]) -> None:
    for target in sorted(graph.get(path[-1], set())):
        if target == path[0]:
            cycles.add(_rotate(path))
        elif target not in path and target > path[0]:
            _walk(graph, [*path, target], cycles)


def _rotate(path: list[str]) -> tuple[str, ...]:
    smallest = path.index(min(path))
    return tuple(path[smallest:] + path[:smallest])


def run(root: Path) -> list[Finding]:
    """Check the .NET project graph and the worker import graph below ``root``."""
    findings = check_edges(read_dotnet_edges(root / "src", root), DOTNET_ALLOWED)
    package_dir = root / "worker" / "src" / PYTHON_PACKAGE
    if package_dir.is_dir():
        findings.extend(check_edges(read_python_edges(package_dir, root), PYTHON_ALLOWED))
    return findings


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    args = build_parser(__doc__, with_paths=False).parse_args(argv)
    findings = run(args.root)
    errors = count_errors(findings)
    lines = [format_findings(findings)] if findings else []
    lines.append(f"dependency_check: {errors} errors, {len(findings) - errors} warnings")
    sys.stdout.write("\n".join(lines) + "\n")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
