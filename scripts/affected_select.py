"""Affected selection: which test-all step groups and .NET projects a change needs (proposal 001).

Compares the working tree (local) or ``HEAD`` (CI) with a base commit, maps every changed file to
step groups through ``scripts/affected/components.json`` and narrows the .NET projects with the
pinned local tool ``dotnet-affected`` (``.config/dotnet-tools.json``), unioned with the test
projects the map names for non-.NET inputs. Fail safe: an unmapped file, a changed file on
``.claude/tipoca/affected/always-build.txt``, a missing base, an unreadable map or a tool error
makes the run full. Standard library only (CI runs it with the runner's ``python3``).

Usage::

    python scripts/affected_select.py [--from REF] [--context local|ci] [--full REASON]
        [--format text|lines|github|json] [--out-dir DIR] [--no-dotnet] [--repo DIR]

Exit codes: 0 = selection printed (affected or full), 166 = no changed file (nothing to run, as
dotnet-affected), 2 = usage error.
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass, field, replace
from pathlib import Path

EXIT_NOTHING = 166
"""Exit code when nothing changed (the same code dotnet-affected uses)."""
EXIT_USAGE = 2
"""Exit code of a usage error (argparse)."""
AFFECTED, FULL, NOTHING = "affected", "full", "nothing"
"""Selection modes."""
SOLUTION = "StemStudio.slnx"
"""The solution a full .NET run builds; also the filter dotnet-affected reads."""
MAP_FILE = "scripts/affected/components.json"
ALWAYS_BUILD_FILE = ".claude/tipoca/affected/always-build.txt"
DEFAULT_ALWAYS_BUILD = (
    "global.json",
    "nuget.config",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    ".config/dotnet-tools.json",
    "dotnet-tools.json",
    ".github/workflows/**",
)
"""Tipoca's default always-build list, used when the project has none."""
MAX_NAMED = 3
"""Unmapped files named in a reason before the rest is counted."""

Runner = Callable[[Sequence[str], Path], "subprocess.CompletedProcess[str]"]
"""Runs a command (program name first) in a folder and returns its captured result."""


class SelectionError(Exception):
    """The analysis cannot be trusted (git, tool or base problem): the run becomes full."""


class MapError(SelectionError):
    """The component map is missing, unreadable or inconsistent."""


_GLOB_TOKENS = re.compile(r"\*\*/|\*\*|\*|\?|[^*?]+")
_GLOB_SPECIAL = {"**/": "(?:.*/)?", "**": ".*", "*": "[^/]*", "?": "[^/]"}


def glob_regex(pattern: str) -> re.Pattern[str]:
    """Compiles a git-pathspec-like glob (case-insensitive; no '/' = file name in any folder)."""
    body = pattern.strip().lstrip("/")
    body = body + "**" if body.endswith("/") else body
    prefix = "" if "/" in body else "(?:.*/)?"
    regex = "".join(_GLOB_SPECIAL.get(t, re.escape(t)) for t in _GLOB_TOKENS.findall(body))
    return re.compile(prefix + regex + r"\Z", re.IGNORECASE)


@dataclass(frozen=True, slots=True)
class Rule:
    """One map rule: path globs, the step groups they select and extra .NET test projects."""

    name: str
    patterns: tuple[re.Pattern[str], ...]
    groups: tuple[str, ...]
    dotnet_projects: tuple[str, ...] = ()


@dataclass(frozen=True, slots=True)
class ComponentMap:
    """The parsed ``components.json``."""

    groups: dict[str, str]
    always: tuple[str, ...]
    subgroups: dict[str, str]
    rules: tuple[Rule, ...]
    hints: dict[str, tuple[str, ...]] = field(default_factory=dict)


def _read_json(path: Path) -> dict[str, object]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise MapError(f"cannot read {path.name}: {error}") from error
    if not isinstance(data, dict) or data.get("version") != 1:
        raise MapError(f"{path.name} must be an object with version 1")
    return data


def _strs(value: object) -> tuple[str, ...]:
    return tuple(str(v) for v in value) if isinstance(value, list) else ()


def _str_map(value: object) -> dict[str, str]:
    return {str(k): str(v) for k, v in value.items()} if isinstance(value, dict) else {}


def _parse_rule(raw: object) -> Rule:
    entry = raw if isinstance(raw, dict) else {}
    name = str(entry.get("name", "?"))
    paths = _strs(entry.get("paths"))
    if not paths:
        raise MapError(f"rule '{name}' has no paths")
    patterns = tuple(glob_regex(p) for p in paths)
    return Rule(name, patterns, _strs(entry.get("groups")), _strs(entry.get("dotnetProjects")))


def _hints(value: object) -> dict[str, tuple[str, ...]]:
    keywords = value.get("keywords") if isinstance(value, dict) else None
    return {str(k): _strs(v) for k, v in keywords.items()} if isinstance(keywords, dict) else {}


def _check_group(group: str, cmap: ComponentMap) -> None:
    if group not in cmap.groups:
        raise MapError(f"unknown group '{group}'")
    if group in cmap.subgroups:
        raise MapError(f"group '{group}' is derived from the .NET projects; no rule may name it")


def _check_groups(cmap: ComponentMap) -> None:
    for group in [*cmap.always, *(g for rule in cmap.rules for g in rule.groups)]:
        _check_group(group, cmap)


def load_map(path: Path) -> ComponentMap:
    """Reads and checks the component map; raises :class:`MapError` on any problem."""
    data = _read_json(path)
    rules = data.get("rules")
    if not isinstance(rules, list):
        raise MapError("rules must be a list")
    cmap = ComponentMap(
        groups=_str_map(data.get("groups")),
        always=_strs(data.get("always")),
        subgroups=_str_map(data.get("dotnetSubgroups")),
        rules=tuple(_parse_rule(r) for r in rules),
        hints=_hints(data.get("e2eHints")),
    )
    _check_groups(cmap)
    return cmap


def load_always_build(path: Path) -> tuple[str, ...]:
    """Reads the always-build globs (skips ``#`` comments and blank lines); defaults if absent."""
    if not path.is_file():
        return DEFAULT_ALWAYS_BUILD
    lines = (line.strip() for line in path.read_text(encoding="utf-8").splitlines())
    return tuple(line for line in lines if line[:1] not in ("", "#"))


def always_build_hit(files: Sequence[str], patterns: Sequence[str]) -> str | None:
    """Names the first changed file on the always-build list as ``file (pattern)``, else None."""
    compiled = [(p, glob_regex(p)) for p in patterns]
    for path in files:
        for pattern, regex in compiled:
            if regex.match(path):
                return f"{path} ({pattern})"
    return None


def rules_for(path: str, cmap: ComponentMap) -> list[Rule]:
    """The rules whose globs match a repository path."""
    return [rule for rule in cmap.rules if any(p.match(path) for p in rule.patterns)]


@dataclass(frozen=True, slots=True)
class Selection:
    """What to run: mode, why, and per selected group the files (or reason) that selected it."""

    mode: str
    reason: str
    groups: dict[str, list[str]]
    dotnet_projects: tuple[str, ...] = ()
    files: tuple[str, ...] = ()
    e2e_tags: tuple[str, ...] = ()
    notes: tuple[str, ...] = ()
    base: str = ""


def full_selection(cmap: ComponentMap | None, reason: str) -> Selection:
    """Selects every group (all groups unknown when the map itself failed)."""
    groups = {g: [reason] for g in cmap.groups} if cmap else {}
    return Selection(FULL, reason, groups)


def _unmapped_reason(unmapped: Sequence[str]) -> str:
    named = ", ".join(unmapped[:MAX_NAMED])
    rest = len(unmapped) - MAX_NAMED
    return f"unmapped file: {named}" + (f" (+{rest} more)" if rest > 0 else "")


def _e2e_tags(files: Sequence[str], hints: dict[str, tuple[str, ...]]) -> tuple[str, ...]:
    text = "\n".join(files).lower()
    hit = [values for key, values in hints.items() if key in text]
    return tuple(sorted({tag for values in hit for tag in values}))


def _collect(
    files: Sequence[str], cmap: ComponentMap
) -> tuple[dict[str, list[str]], set[str], list[str]]:
    groups: dict[str, list[str]] = {}
    projects: set[str] = set()
    unmapped: list[str] = []
    for path in files:
        rules = rules_for(path, cmap)
        unmapped += [] if rules else [path]
        for rule in rules:
            projects.update(rule.dotnet_projects)
            for group in rule.groups:
                groups.setdefault(group, []).append(path)
    return groups, projects, unmapped


def _full_reason(files: Sequence[str], always_build: Sequence[str], unmapped: list[str]) -> str:
    hit = always_build_hit(files, always_build)
    if hit:
        return f"always-build file changed: {hit}"
    return _unmapped_reason(unmapped) if unmapped else ""


def select_groups(
    files: Sequence[str], cmap: ComponentMap, always_build: Sequence[str]
) -> Selection:
    """Maps changed files to groups; full for an always-build file or an unmapped file."""
    if not files:
        return Selection(NOTHING, "no changed file", {})
    groups, projects, unmapped = _collect(files, cmap)
    full_reason = _full_reason(files, always_build, unmapped)
    if full_reason:
        return replace(full_selection(cmap, full_reason), files=tuple(files))
    groups.update({g: ["always"] for g in cmap.always})
    reason = f"{len(files)} changed file(s)"
    tags = _e2e_tags(files, cmap.hints)
    return Selection(AFFECTED, reason, groups, tuple(sorted(projects)), tuple(files), tags)


def apply_dotnet(
    selection: Selection, tool_projects: Sequence[str], cmap: ComponentMap
) -> Selection:
    """Unions the dotnet-affected projects with the mapped ones and derives the .NET subgroups."""
    if selection.mode != AFFECTED or "dotnet" not in selection.groups:
        return selection
    projects = tuple(sorted({*tool_projects, *selection.dotnet_projects}))
    groups = dict(selection.groups)
    if not projects:
        del groups["dotnet"]
        note = "dotnet: no affected project (dotnet-affected exit 166)"
        return replace(selection, groups=groups, notes=(*selection.notes, note))
    groups.update(_subgroups(cmap, projects))
    return replace(selection, groups=groups, dotnet_projects=projects)


def _subgroups(cmap: ComponentMap, projects: Sequence[str]) -> dict[str, list[str]]:
    return {sub: [proj] for sub, proj in cmap.subgroups.items() if proj in projects}


def _program(name: str) -> str:
    found = shutil.which(name)
    if not found or not Path(found).is_absolute():
        raise SelectionError(f"{name} not found on PATH")
    return found


def run_command(args: Sequence[str], cwd: Path) -> "subprocess.CompletedProcess[str]":
    """Runs a program from PATH (resolved to an absolute path) and captures its output."""
    program = _program(args[0])
    return subprocess.run(  # noqa: S603 - fixed programs, arguments are not shell-interpreted
        [program, *args[1:]], cwd=cwd, capture_output=True, text=True, check=False
    )


def _git(repo: Path, run: Runner, *args: str) -> "subprocess.CompletedProcess[str]":
    return run(["git", "-c", "core.quotepath=off", *args], repo)


def resolve_base(repo: Path, ref: str | None, run: Runner) -> str:
    """The base commit: ``ref`` itself, else the merge-base of HEAD with main (or origin/main)."""
    if ref is None:
        for branch in ("main", "origin/main"):
            done = _git(repo, run, "merge-base", branch, "HEAD")
            if done.returncode == 0:
                return done.stdout.strip()
        raise SelectionError("no merge-base with main or origin/main (pass --from)")
    done = _git(repo, run, "rev-parse", "--verify", "--quiet", f"{ref}^{{commit}}")
    if done.returncode != 0:
        raise SelectionError(f"base '{ref}' is not a commit in this clone")
    return done.stdout.strip()


def _paths(done: "subprocess.CompletedProcess[str]", what: str) -> set[str]:
    if done.returncode != 0:
        raise SelectionError(f"{what} failed: {done.stderr.strip()[:200]}")
    return {p for p in done.stdout.split("\0") if p}


def changed_files(repo: Path, base: str, context: str, run: Runner) -> list[str]:
    """Changed paths since ``base``: CI compares commits; local adds uncommitted and untracked."""
    target = ["HEAD"] if context == "ci" else []
    diff = _git(repo, run, "diff", "--name-only", "--no-renames", "-z", base, *target)
    files = _paths(diff, "git diff")
    if context != "ci":
        untracked = _git(repo, run, "ls-files", "--others", "--exclude-standard", "-z")
        files |= _paths(untracked, "git ls-files")
    return sorted(files)


@dataclass(frozen=True, slots=True)
class Options:
    """Command-line options."""

    repo: Path
    context: str = "local"
    out_dir: Path = Path()
    base_ref: str | None = None
    fmt: str = "text"
    dotnet: bool = True
    full_reason: str | None = None


def _tool_command(options: Options, base: str) -> list[str]:
    uncommitted = "None" if options.context == "ci" else "All"
    repo = str(options.repo)
    return [
        "dotnet", "tool", "run", "dotnet-affected", "--repository-path", repo,
        "--filter-file-path", str(options.repo / SOLUTION), "--from", base,
        "--uncommitted", uncommitted, "--format", "json",
        "--output-dir", str(options.out_dir), "--output-name", "dotnet-affected",
    ]  # fmt: skip


def _tool_projects(options: Options) -> tuple[str, ...]:
    try:
        entries = json.loads((options.out_dir / "dotnet-affected.json").read_text(encoding="utf-8"))
        paths = [Path(str(entry["FilePath"]).replace("\\", "/")) for entry in entries]
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise SelectionError(f"no dotnet-affected.json ({error})") from error
    return tuple(sorted(p.resolve().relative_to(options.repo.resolve()).as_posix() for p in paths))


def dotnet_projects(options: Options, base: str, run: Runner) -> tuple[str, ...]:
    """Runs the pinned dotnet-affected; () for exit 166, :class:`SelectionError` on any failure."""
    options.out_dir.mkdir(parents=True, exist_ok=True)
    (options.out_dir / "dotnet-affected.json").unlink(missing_ok=True)
    if run(["dotnet", "tool", "restore"], options.repo).returncode != 0:
        raise SelectionError("dotnet tool restore failed")
    done = run(_tool_command(options, base), options.repo)
    if done.returncode == EXIT_NOTHING:
        return ()
    if done.returncode != 0:
        raise SelectionError(f"dotnet-affected exit {done.returncode}: {done.stderr.strip()[:200]}")
    return _tool_projects(options)


def write_filter(out_dir: Path, repo: Path, projects: Sequence[str]) -> Path:
    """Writes ``affected.slnf``, a solution filter of StemStudio.slnx with the given projects."""
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / "affected.slnf"
    solution = Path(os.path.relpath(repo / SOLUTION, out_dir)).as_posix()
    document = {"solution": {"path": solution, "projects": list(projects)}}
    path.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    return path


def dotnet_target(selection: Selection, options: Options) -> str:
    """The .NET build input: the solution (full or no analysis) or the written solution filter."""
    if selection.mode != AFFECTED or not selection.dotnet_projects or not options.dotnet:
        return SOLUTION
    return write_filter(options.out_dir, options.repo, selection.dotnet_projects).as_posix()


def _skip_reason(group: str, cmap: ComponentMap | None) -> str:
    return "no affected test project" if cmap and group in cmap.subgroups else "no changed input"


def format_lines(selection: Selection, cmap: ComponentMap | None, target: str) -> list[str]:
    """Machine lines for test-all: mode, reason, ``group <id> run|skip <why>`` lines, .NET."""
    lines = [f"mode {selection.mode}", f"reason {selection.reason}"]
    for group in cmap.groups if cmap else selection.groups:
        why = selection.groups.get(group)
        lines.append(
            f"group {group} run {why[0]}"
            if why
            else f"group {group} skip {_skip_reason(group, cmap)}"
        )
    lines.append(f"dotnet target {target}")
    lines += [f"dotnet project {p}" for p in selection.dotnet_projects]
    return lines


def format_github(selection: Selection, cmap: ComponentMap | None, target: str) -> list[str]:
    """``$GITHUB_OUTPUT`` lines: mode, reason, ``dotnet-target`` and ``<group>=true|false``."""
    lines = [f"mode={selection.mode}", f"reason={selection.reason}", f"dotnet-target={target}"]
    names = cmap.groups if cmap else selection.groups
    return lines + [f"{g}={'true' if g in selection.groups else 'false'}" for g in names]


def format_text(selection: Selection, cmap: ComponentMap | None) -> list[str]:
    """Human report: mode and reason, run/skip per group with why, .NET projects, E2E hints."""
    base = f", base {selection.base[:12]}" * bool(selection.base)
    lines = [f"affected selection: {selection.mode} ({selection.reason}{base})"]
    lines += _group_rows(format_lines(selection, cmap, ""))
    lines += [f"  .NET project: {p}" for p in selection.dotnet_projects]
    lines += [f"  note: {n}" for n in selection.notes]
    tags = [f"  E2E tags probably touched (report only): {' '.join(selection.e2e_tags)}"]
    return lines + tags[: len(selection.e2e_tags)]


def _group_rows(machine_lines: Sequence[str]) -> list[str]:
    rows = [line.split(" ", 3) for line in machine_lines if line.startswith("group ")]
    return [f"  {verb:<5} {group:<24} {why}" for _, group, verb, why in rows]


def format_json(selection: Selection, target: str) -> list[str]:
    """The selection as one JSON document."""
    keys = ("mode", "reason", "base", "groups", "dotnet_projects", "files", "e2e_tags", "notes")
    document = {k: getattr(selection, k) for k in keys} | {"dotnet_target": target}
    return [json.dumps(document, indent=2)]


def _analyse(options: Options, cmap: ComponentMap, run: Runner) -> Selection:
    base = resolve_base(options.repo, options.base_ref, run)
    files = changed_files(options.repo, base, options.context, run)
    always = load_always_build(options.repo / ALWAYS_BUILD_FILE)
    selection = replace(select_groups(files, cmap, always), base=base)
    if selection.mode != AFFECTED or "dotnet" not in selection.groups:
        return selection
    if options.dotnet:
        return apply_dotnet(selection, dotnet_projects(options, base, run), cmap)
    unknown = {sub: ["no .NET analysis (--no-dotnet)"] for sub in cmap.subgroups}
    return replace(selection, groups=selection.groups | unknown)


def select(options: Options, run: Runner) -> tuple[Selection, ComponentMap | None]:
    """The whole analysis, fail safe: any :class:`SelectionError` gives a full selection."""
    cmap: ComponentMap | None = None
    try:
        cmap = load_map(options.repo / MAP_FILE)
        if options.full_reason:
            return full_selection(cmap, options.full_reason), cmap
        return _analyse(options, cmap, run), cmap
    except SelectionError as error:
        return full_selection(cmap, f"analysis failed: {error}"), cmap


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--from", dest="base_ref", help="base ref (default: merge-base with main)")
    parser.add_argument("--context", choices=("local", "ci"), default="local")
    parser.add_argument("--full", dest="full_reason", help="select everything, with this reason")
    formats = ("text", "lines", "github", "json")
    parser.add_argument("--format", dest="fmt", choices=formats, default="text")
    parser.add_argument("--out-dir", type=Path, help="affected.slnf folder (.claude/scratchpad)")
    parser.add_argument(
        "--no-dotnet", dest="dotnet", action="store_false", help="no dotnet-affected"
    )
    return parser


def parse_options(argv: Sequence[str]) -> Options:
    """Parses the command line into :class:`Options` (argparse exits 2 on a usage error)."""
    values = vars(_parser().parse_args(argv))
    values["repo"] = values["repo"].resolve()
    values["out_dir"] = (
        values["out_dir"] or values["repo"] / ".claude" / "scratchpad" / "affected"
    ).resolve()
    return Options(**values)


def render(selection: Selection, cmap: ComponentMap | None, options: Options) -> list[str]:
    """The output lines in the requested format."""
    target = dotnet_target(selection, options)
    if options.fmt == "lines":
        return format_lines(selection, cmap, target)
    if options.fmt == "github":
        return format_github(selection, cmap, target)
    if options.fmt == "json":
        return format_json(selection, target)
    return format_text(selection, cmap)


def main(argv: Sequence[str] | None = None, run: Runner = run_command) -> int:
    """Command-line entry point; returns the exit code."""
    options = parse_options(sys.argv[1:] if argv is None else argv)
    selection, cmap = select(options, run)
    print("\n".join(render(selection, cmap, options)))  # noqa: T201 - command line output
    return EXIT_NOTHING if selection.mode == NOTHING else 0


if __name__ == "__main__":
    sys.exit(main())
