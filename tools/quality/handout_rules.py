"""Insights and handouts rules (feature 020, tasks.md T009/T010; FR-012, FR-018, research P0-4).

(a) Style drift: the handout copy of the app's light-theme tokens and the Archivo font
    (``worker/src/stemstudio_worker/handouts/assets/``) equals what the app sources give:
    ``worker/scripts/sync_handout_style.py --check`` exits 0. A tree without the script is not
    checked.
(b) No literal colours in the handout style sheets (``handouts/styles/**/*.css``): the finder of
    ``css_tokens_check`` (hex, colour functions, named colours); only ``var(--ss-*)`` tokens of
    the copied ``assets/tokens.css`` (which may hold colours) are allowed.
(c) No network while rendering: every ``url_fetcher`` the handout package passes is an instance
    of a ``URLFetcher`` subclass defined in ``handouts/fetcher.py`` (a call of that class, a name
    or attribute assigned from such a call, or a name annotated with that class or
    ``URLFetcher``), never a plain function, a lambda or WeasyPrint's default fetcher; and every
    ``HTML(...)``/``CSS(...)`` call passes one explicitly (``**options`` cannot be proven).

The worker layering of T009 (``insights``, ``handouts``) is part of ``dependency_check``.

Usage::

    python tools/quality/handout_rules.py [--root DIR]

Exit code 0 = clean, 1 = a rule is broken.
"""

import ast
import subprocess  # nosec B404 - runs the repository's own drift script with this interpreter
import sys
from collections.abc import Iterator, Sequence
from pathlib import Path
from typing import Final

from common import Finding, Severity, build_parser, files_below, relative, report_findings
from css_tokens_check import find_css_colours

CHECK: Final = "handouts"
"""Check name of the drift and fetcher findings (colours use ``raw-colour``)."""

SYNC_SCRIPT: Final = "worker/scripts/sync_handout_style.py"
HANDOUTS: Final = "worker/src/stemstudio_worker/handouts"
STYLES: Final = f"{HANDOUTS}/styles"
FETCHER_MODULE: Final = "fetcher.py"
BASE_FETCHER: Final = "URLFetcher"
RENDER_CALLS: Final = frozenset({"HTML", "CSS"})
"""WeasyPrint constructors that fetch resources; each must get an explicit ``url_fetcher``."""

SYNC_COMMAND: Final = f"uv run --project worker python {SYNC_SCRIPT}"
SYNC_TIMEOUT_SECONDS: Final = 60
_RULE: Final = " (FR-018, research P0-4)"


def run(root: Path) -> list[Finding]:
    """Check the style copy, the handout style sheets and the fetchers below ``root``."""
    return [*check_drift(root), *check_colours(root), *check_fetchers(root)]


# ------------------------------------------------------------------ (a) style drift


def check_drift(root: Path) -> list[Finding]:
    """(a) One finding when ``sync_handout_style.py --check`` reports drift or fails."""
    script = root / SYNC_SCRIPT
    if not script.is_file():
        return []
    command = [sys.executable, "-I", str(script), "--check", "--root", str(root)]
    done = subprocess.run(  # noqa: S603 - fixed argument list, no shell  # nosec B603
        command, capture_output=True, text=True, timeout=SYNC_TIMEOUT_SECONDS, check=False
    )
    if done.returncode == 0:
        return []
    return [Finding(CHECK, Severity.ERROR, SYNC_SCRIPT, 0, _drift_message(done))]


def _drift_message(done: subprocess.CompletedProcess[str]) -> str:
    out = done.stdout.strip()
    if out.startswith("handout assets out of date"):
        return f"{'; '.join(out.splitlines())}; run {SYNC_COMMAND}"
    last = (done.stderr.strip().splitlines() or ["no output"])[-1]
    return f"sync_handout_style.py --check failed (exit {done.returncode}): {last}"


# ------------------------------------------------------------------ (b) literal colours


def check_colours(root: Path) -> list[Finding]:
    """(b) One finding per literal colour in a handout style sheet."""
    findings = []
    for path in files_below(root / STYLES, ".css"):
        rel_path = relative(path, root)
        for hit in find_css_colours(path.read_text(encoding="utf-8", errors="replace")):
            message = (
                f"raw colour `{hit.literal}` in a handout style sheet; use a handout token "
                "var(--ss-...) from assets/tokens.css (FR-012)"
            )
            findings.append(Finding("raw-colour", Severity.ERROR, rel_path, hit.line, message, 1))
    return findings


# ------------------------------------------------------------------ (c) url_fetcher


def check_fetchers(root: Path) -> list[Finding]:
    """(c) Plain or missing ``url_fetcher`` arguments in the handout package."""
    package = root / HANDOUTS
    classes = fetcher_classes(package / FETCHER_MODULE)
    findings: list[Finding] = []
    for path in files_below(package, ".py"):
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        rel_path = relative(path, root)
        safe = _safe_expressions(tree, classes)
        findings.extend(
            Finding(CHECK, Severity.ERROR, rel_path, line, message + _RULE)
            for line, message in _fetcher_breaches(tree, classes, safe)
        )
    return findings


def fetcher_classes(module: Path) -> frozenset[str]:
    """Names of the classes in ``module`` that subclass ``URLFetcher`` (directly or not)."""
    if not module.is_file():
        return frozenset()
    tree = ast.parse(module.read_text(encoding="utf-8"), filename=str(module))
    found: set[str] = set()
    for node in tree.body:
        if isinstance(node, ast.ClassDef) and _subclasses(node, found | {BASE_FETCHER}):
            found.add(node.name)
    return frozenset(found)


def _subclasses(node: ast.ClassDef, bases: set[str]) -> bool:
    return any(_name(base) in bases for base in node.bases)


def _name(node: ast.expr) -> str:
    """Last name of a ``Name`` or ``Attribute`` (``weasyprint.urls.URLFetcher``: ``URLFetcher``)."""
    if isinstance(node, ast.Name):
        return node.id
    return node.attr if isinstance(node, ast.Attribute) else ""


def _is_fetcher_call(node: ast.expr | None, classes: frozenset[str]) -> bool:
    return isinstance(node, ast.Call) and _name(node.func) in classes


def _safe_expressions(tree: ast.Module, classes: frozenset[str]) -> frozenset[str]:
    """Source texts of names and attributes that hold a fetcher instance."""
    annotated = classes | {BASE_FETCHER}
    return frozenset(
        text for node in ast.walk(tree) for text in _safe_targets(node, classes, annotated)
    )


def _safe_targets(node: ast.AST, classes: frozenset[str], annotated: frozenset[str]) -> list[str]:
    """Targets ``node`` binds to a fetcher: ``x = Fetcher()``, ``x: Fetcher``, ``x: URLFetcher``."""
    if isinstance(node, ast.Assign):
        return _assigned(node, classes)
    if isinstance(node, ast.AnnAssign):
        return _annotated(node, classes, annotated)
    if isinstance(node, ast.arg):
        return _typed_argument(node, annotated)
    return []


def _assigned(node: ast.Assign, classes: frozenset[str]) -> list[str]:
    return [ast.unparse(t) for t in node.targets] if _is_fetcher_call(node.value, classes) else []


def _annotated(
    node: ast.AnnAssign, classes: frozenset[str], annotated: frozenset[str]
) -> list[str]:
    typed = _name(node.annotation) in annotated or _is_fetcher_call(node.value, classes)
    return [ast.unparse(node.target)] if typed else []


def _typed_argument(node: ast.arg, annotated: frozenset[str]) -> list[str]:
    typed = node.annotation is not None and _name(node.annotation) in annotated
    return [node.arg] if typed else []


def _fetcher_breaches(
    tree: ast.Module, classes: frozenset[str], safe: frozenset[str]
) -> Iterator[tuple[int, str]]:
    for node in ast.walk(tree):
        message = _call_breach(node, classes, safe) if isinstance(node, ast.Call) else None
        if message is not None:
            yield getattr(node, "lineno", 0), message


def _call_breach(node: ast.Call, classes: frozenset[str], safe: frozenset[str]) -> str | None:
    """The breach of one call: a plain ``url_fetcher`` or a render call without one."""
    fetcher = _keyword(node, "url_fetcher")
    if fetcher is None:
        return _missing_fetcher(_name(node.func))
    if _is_fetcher_call(fetcher, classes) or ast.unparse(fetcher) in safe:
        return None
    return (
        f"url_fetcher `{ast.unparse(fetcher)}` is not an instance of the URLFetcher subclass "
        "of handouts/fetcher.py"
    )


def _keyword(node: ast.Call, name: str) -> ast.expr | None:
    return next((k.value for k in node.keywords if k.arg == name), None)


def _missing_fetcher(name: str) -> str | None:
    if name not in RENDER_CALLS:
        return None
    return f"{name}(...) without url_fetcher uses the default (network) fetcher"


# ------------------------------------------------------------------ command line


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    root: Path = build_parser(__doc__, with_paths=False).parse_args(argv).root
    return report_findings("handout_rules", run(root))


if __name__ == "__main__":
    sys.exit(main())
