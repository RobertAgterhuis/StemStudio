"""Tests for scripts/affected_select.py (refactor proposal 001: build and test what changed)."""

import json
import re
import subprocess
from collections.abc import Sequence
from pathlib import Path

import pytest

import affected_select as sel

REPO_ROOT = Path(__file__).resolve().parents[2]
MAP_PATH = REPO_ROOT / "scripts" / "affected" / "components.json"
ALWAYS_BUILD = REPO_ROOT / ".claude" / "tipoca" / "affected" / "always-build.txt"
DOMAIN_TESTS = "tests/StemStudio.Domain.Tests/StemStudio.Domain.Tests.csproj"
AGENT_TESTS = "tests/StemStudio.Agent.Tests/StemStudio.Agent.Tests.csproj"
API_TESTS = "tests/StemStudio.Api.Tests/StemStudio.Api.Tests.csproj"

SMALL_MAP = {
    "version": 1,
    "groups": {
        "dotnet": "d",
        "worker": "w",
        "docs": "m",
        "secret-scan": "s",
        "dotnet-agent-floor": "f",
    },
    "always": ["secret-scan"],
    "dotnetSubgroups": {"dotnet-agent-floor": AGENT_TESTS},
    "rules": [
        {"name": "net", "paths": ["src/StemStudio.*/**"], "groups": ["dotnet"]},
        {
            "name": "contracts",
            "paths": ["specs/**/contracts/**"],
            "groups": ["dotnet", "worker"],
            "dotnetProjects": [DOMAIN_TESTS],
        },
        {"name": "worker", "paths": ["worker/**"], "groups": ["worker"]},
        {"name": "manuals", "paths": ["docs/public/**"], "groups": ["docs"]},
        {"name": "notes", "paths": ["docs/handoffs/**", "*.txt"], "groups": []},
    ],
    "e2eHints": {"keywords": {"agent": ["@agent"]}},
}


def write_map(tmp_path: Path, data: object) -> Path:
    path = tmp_path / "components.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


@pytest.fixture
def small_map(tmp_path: Path) -> sel.ComponentMap:
    return sel.load_map(write_map(tmp_path, SMALL_MAP))


# --- globs ------------------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("pattern", "path", "expected"),
    [
        ("worker/**", "worker/src/a/b.py", True),
        ("worker/**", "workers/a.py", False),
        ("specs/**/contracts/**", "specs/016-x/contracts/a.yaml", True),
        ("specs/**/contracts/**", "specs/011-x/followups/k/contracts/a.yaml", True),
        ("specs/*/evaluation/**", "specs/023-x/evaluation/set.md", True),
        ("specs/*/evaluation/**", "specs/023-x/y/evaluation/set.md", False),
        ("src/StemStudio.*/**", "src/StemStudio.Domain/A.cs", True),
        ("src/StemStudio.*/**", "src/audio-engine/a.ts", False),
        ("docker-compose*.yml", "docker-compose.gpu.yml", True),
        ("Directory.Packages.props", "directory.packages.props", True),
        ("global.json", "sub/dir/global.json", True),
        ("/global.json", "global.json", True),
        ("scripts/build-soundtouch.*", "scripts/build-soundtouch.ps1", True),
        ("a?c", "abc", True),
        ("a?c", "a/c", False),
        ("docs/handoffs/", "docs/handoffs/x.md", True),
        ("file[1].txt", "file[1].txt", True),
    ],
)
def test_glob_regex_matches_like_git_pathspecs(pattern: str, path: str, expected: bool) -> None:
    assert bool(sel.glob_regex(pattern).match(path)) is expected


# --- component map ----------------------------------------------------------------------------


def test_load_map_reads_groups_rules_and_subgroups(small_map: sel.ComponentMap) -> None:
    assert "worker" in small_map.groups
    assert small_map.always == ("secret-scan",)
    assert small_map.subgroups == {"dotnet-agent-floor": AGENT_TESTS}
    assert [rule.name for rule in small_map.rules][:2] == ["net", "contracts"]
    assert small_map.rules[1].dotnet_projects == (DOMAIN_TESTS,)


@pytest.mark.parametrize(
    ("change", "message"),
    [
        ({"rules": [{"name": "x", "paths": ["a/**"], "groups": ["nope"]}]}, "unknown group 'nope'"),
        ({"always": ["nope"]}, "unknown group 'nope'"),
        ({"rules": [{"name": "x", "paths": [], "groups": []}]}, "rule 'x' has no paths"),
        (
            {"rules": [{"name": "x", "paths": ["a"], "groups": ["dotnet-agent-floor"]}]},
            "derived from the .NET projects",
        ),
        ({"rules": "x"}, "rules must be a list"),
        ({"version": 2}, "version 1"),
    ],
)
def test_load_map_rejects_inconsistent_maps(
    tmp_path: Path, change: dict[str, object], message: str
) -> None:
    with pytest.raises(sel.MapError, match=re.escape(message)):
        sel.load_map(write_map(tmp_path, {**SMALL_MAP, **change}))


def test_load_map_rejects_missing_and_broken_files(tmp_path: Path) -> None:
    with pytest.raises(sel.MapError, match="cannot read"):
        sel.load_map(tmp_path / "missing.json")
    broken = tmp_path / "broken.json"
    broken.write_text("{", encoding="utf-8")
    with pytest.raises(sel.MapError, match="cannot read"):
        sel.load_map(broken)


def test_the_repository_map_is_valid_and_covers_every_tracked_file() -> None:
    cmap = sel.load_map(MAP_PATH)
    tracked = git(REPO_ROOT, "ls-files").splitlines()
    always = sel.load_always_build(ALWAYS_BUILD)
    unmapped = [
        f for f in tracked if not sel.rules_for(f, cmap) and not sel.always_build_hit([f], always)
    ]
    assert unmapped == []


# --- always-build list ------------------------------------------------------------------------


def test_load_always_build_skips_comments_and_blank_lines(tmp_path: Path) -> None:
    path = tmp_path / "always-build.txt"
    path.write_text(
        "# comment\n\nglobal.json\r\n  tests/coverlet.runsettings  \n", encoding="utf-8"
    )
    assert sel.load_always_build(path) == ("global.json", "tests/coverlet.runsettings")


def test_load_always_build_falls_back_to_the_defaults(tmp_path: Path) -> None:
    patterns = sel.load_always_build(tmp_path / "missing.txt")
    assert "Directory.Packages.props" in patterns
    assert ".github/workflows/**" in patterns


def test_the_repository_always_build_list_names_the_repo_wide_inputs() -> None:
    patterns = sel.load_always_build(ALWAYS_BUILD)
    for required in [
        "Directory.Packages.props",
        "Directory.Build.props",
        "global.json",
        ".editorconfig",
        "StemStudio.slnx",
        ".config/dotnet-tools.json",
        "tests/coverlet.runsettings",
        "scripts/affected/**",
    ]:
        assert required in patterns


def test_always_build_hit_names_the_file_and_the_pattern() -> None:
    hit = sel.always_build_hit(["a.md", "src/Directory.Build.props"], ("Directory.Build.props",))
    assert hit == "src/Directory.Build.props (Directory.Build.props)"
    assert sel.always_build_hit(["a.md"], ("global.json",)) is None


# --- group selection --------------------------------------------------------------------------


def test_select_without_changes_selects_nothing(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups([], small_map, ())
    assert result.mode == sel.NOTHING
    assert result.groups == {}


def test_select_maps_a_worker_file_to_worker_and_the_always_groups(
    small_map: sel.ComponentMap,
) -> None:
    result = sel.select_groups(["worker/src/a.py"], small_map, ())
    assert result.mode == sel.AFFECTED
    assert set(result.groups) == {"worker", "secret-scan"}
    assert result.groups["worker"] == ["worker/src/a.py"]
    assert result.groups["secret-scan"] == ["always"]


def test_select_follows_cross_component_edges(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups(["specs/016-x/contracts/a.yaml"], small_map, ())
    assert {"dotnet", "worker"} <= set(result.groups)
    assert result.dotnet_projects == (DOMAIN_TESTS,)


def test_select_maps_documentation_to_no_group(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups(["docs/handoffs/x.md", "notes.txt"], small_map, ())
    assert result.mode == sel.AFFECTED
    assert set(result.groups) == {"secret-scan"}


def test_select_runs_everything_for_an_unmapped_file(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups(["worker/a.py", "mystery/file.bin"], small_map, ())
    assert result.mode == sel.FULL
    assert "unmapped file: mystery/file.bin" in result.reason
    assert set(result.groups) == set(small_map.groups)


def test_select_runs_everything_for_an_always_build_file(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups(["worker/a.py", "global.json"], small_map, ("global.json",))
    assert result.mode == sel.FULL
    assert result.reason == "always-build file changed: global.json (global.json)"


def test_select_counts_more_unmapped_files(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups(["x/1", "x/2", "x/3", "x/4"], small_map, ())
    assert result.reason == "unmapped file: x/1, x/2, x/3 (+1 more)"


def test_e2e_hints_are_report_only(small_map: sel.ComponentMap) -> None:
    result = sel.select_groups(["src/StemStudio.Agent/A.cs"], small_map, ())
    assert result.e2e_tags == ("@agent",)
    assert "e2e" not in result.groups


# --- .NET projects ----------------------------------------------------------------------------


def test_apply_dotnet_unions_tool_and_mapped_projects(small_map: sel.ComponentMap) -> None:
    base = sel.select_groups(["specs/x/contracts/a.yaml"], small_map, ())
    result = sel.apply_dotnet(base, (AGENT_TESTS,), small_map)
    assert result.dotnet_projects == (AGENT_TESTS, DOMAIN_TESTS)
    assert result.groups["dotnet-agent-floor"] == [AGENT_TESTS]


def test_apply_dotnet_drops_dotnet_when_nothing_is_affected(small_map: sel.ComponentMap) -> None:
    base = sel.select_groups(["src/StemStudio.Web/readme.md"], small_map, ())
    result = sel.apply_dotnet(base, (), small_map)
    assert "dotnet" not in result.groups
    assert result.notes == ("dotnet: no affected project (dotnet-affected exit 166)",)


def test_apply_dotnet_leaves_selections_without_dotnet_alone(small_map: sel.ComponentMap) -> None:
    base = sel.select_groups(["worker/a.py"], small_map, ())
    assert sel.apply_dotnet(base, (AGENT_TESTS,), small_map) == base


def test_full_selection_runs_every_group_and_subgroup(small_map: sel.ComponentMap) -> None:
    result = sel.full_selection(small_map, "manual run")
    assert result.mode == sel.FULL
    assert set(result.groups) == set(small_map.groups)


class FakeRunner:
    """Records commands and answers them from a table of (program, first argument) -> result."""

    def __init__(self, answers: dict[str, tuple[int, str]], tool_json: str | None = None) -> None:
        self.answers = answers
        self.tool_json = tool_json
        self.calls: list[list[str]] = []

    def __call__(self, args: Sequence[str], cwd: Path) -> subprocess.CompletedProcess[str]:
        self.calls.append(list(args))
        key = " ".join(args[1:3])
        code, out = next((v for k, v in self.answers.items() if key.startswith(k)), (0, ""))
        if self.tool_json is not None and "--output-dir" in args:
            out_dir = Path(args[args.index("--output-dir") + 1])
            (out_dir / "dotnet-affected.json").write_text(self.tool_json, encoding="utf-8")
        return subprocess.CompletedProcess(list(args), code, out, "tool said no")


def tool_options(tmp_path: Path, context: str = "local") -> sel.Options:
    return sel.Options(repo=tmp_path, context=context, out_dir=tmp_path / "out")


def test_dotnet_projects_reads_the_tool_json(tmp_path: Path) -> None:
    entry = {"Name": "X", "FilePath": str(tmp_path / "tests" / "X.Tests" / "X.Tests.csproj")}
    runner = FakeRunner({}, json.dumps([entry]))
    projects = sel.dotnet_projects(tool_options(tmp_path), "abc", runner)
    assert projects == ("tests/X.Tests/X.Tests.csproj",)
    tool_call = runner.calls[-1]
    assert tool_call[tool_call.index("--from") + 1] == "abc"
    assert tool_call[tool_call.index("--uncommitted") + 1] == "All"


def test_dotnet_projects_ignores_uncommitted_changes_in_ci(tmp_path: Path) -> None:
    runner = FakeRunner({"tool run": (166, "")})
    assert sel.dotnet_projects(tool_options(tmp_path, "ci"), "abc", runner) == ()
    assert runner.calls[-1][runner.calls[-1].index("--uncommitted") + 1] == "None"


@pytest.mark.parametrize(
    ("answers", "tool_json", "message"),
    [
        ({"tool restore": (1, "")}, None, "dotnet tool restore failed"),
        ({"tool run": (3, "")}, None, "dotnet-affected exit 3"),
        ({}, None, "no dotnet-affected.json"),
        ({}, "{not json", "no dotnet-affected.json"),
    ],
)
def test_dotnet_projects_reports_tool_errors(
    tmp_path: Path, answers: dict[str, tuple[int, str]], tool_json: str | None, message: str
) -> None:
    with pytest.raises(sel.SelectionError, match=re.escape(message)):
        sel.dotnet_projects(tool_options(tmp_path), "abc", FakeRunner(answers, tool_json))


def test_write_filter_writes_a_solution_filter(tmp_path: Path) -> None:
    path = sel.write_filter(tmp_path / "out", tmp_path, (DOMAIN_TESTS,))
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["solution"]["projects"] == [DOMAIN_TESTS]
    assert (path.parent / data["solution"]["path"]).resolve() == (
        tmp_path / "StemStudio.slnx"
    ).resolve()


# --- git ---------------------------------------------------------------------------------------


def git(repo: Path, *args: str) -> str:
    done = subprocess.run(  # noqa: S603 - fixed program, test-only arguments
        ["git", "-C", str(repo), *args],  # noqa: S607 - git from PATH, as in CI
        capture_output=True,
        text=True,
        check=True,
    )
    return done.stdout


@pytest.fixture
def repo(tmp_path: Path) -> Path:
    root = tmp_path / "repo"
    root.mkdir()
    git(root, "init", "-q", "-b", "main")
    git(root, "config", "user.email", "test@example.invalid")
    git(root, "config", "user.name", "test")
    (root / "worker").mkdir()
    (root / "worker" / "a.py").write_text("a\n", encoding="utf-8")
    git(root, "add", ".")
    git(root, "commit", "-q", "-m", "base")
    return root


def commit_file(repo: Path, rel: str, text: str = "x\n") -> None:
    path = repo / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    git(repo, "add", rel)
    git(repo, "commit", "-q", "-m", rel)


def test_resolve_base_defaults_to_the_merge_base_with_main(repo: Path) -> None:
    main_head = git(repo, "rev-parse", "HEAD").strip()
    git(repo, "checkout", "-q", "-b", "feature")
    commit_file(repo, "worker/b.py")
    assert sel.resolve_base(repo, None, sel.run_command) == main_head


def test_resolve_base_accepts_an_explicit_ref(repo: Path) -> None:
    commit_file(repo, "worker/b.py")
    assert (
        sel.resolve_base(repo, "HEAD~1", sel.run_command)
        == git(repo, "rev-parse", "HEAD~1").strip()
    )


def test_resolve_base_rejects_a_missing_commit(repo: Path) -> None:
    with pytest.raises(sel.SelectionError, match="base 'deadbeef' is not a commit"):
        sel.resolve_base(repo, "deadbeef", sel.run_command)


def test_resolve_base_without_main_reports_the_missing_base(repo: Path) -> None:
    git(repo, "branch", "-m", "main", "trunk")
    with pytest.raises(sel.SelectionError, match="no merge-base with main"):
        sel.resolve_base(repo, None, sel.run_command)


def test_changed_files_local_includes_uncommitted_and_untracked(repo: Path) -> None:
    base = git(repo, "rev-parse", "HEAD").strip()
    commit_file(repo, "docs/handoffs/n.md")
    (repo / "worker" / "a.py").write_text("changed\n", encoding="utf-8")
    (repo / "new.txt").write_text("n\n", encoding="utf-8")
    assert sel.changed_files(repo, base, "local", sel.run_command) == [
        "docs/handoffs/n.md",
        "new.txt",
        "worker/a.py",
    ]


def test_changed_files_ci_compares_commits_only(repo: Path) -> None:
    base = git(repo, "rev-parse", "HEAD").strip()
    commit_file(repo, "docs/handoffs/n.md")
    (repo / "new.txt").write_text("n\n", encoding="utf-8")
    assert sel.changed_files(repo, base, "ci", sel.run_command) == ["docs/handoffs/n.md"]


def test_changed_files_reports_git_errors(tmp_path: Path) -> None:
    with pytest.raises(sel.SelectionError, match="git diff failed"):
        sel.changed_files(tmp_path, "HEAD", "ci", sel.run_command)


def test_run_command_rejects_a_missing_program(tmp_path: Path) -> None:
    with pytest.raises(sel.SelectionError, match="not found on PATH"):
        sel.run_command(["no-such-program-affected"], tmp_path)


# --- command line -----------------------------------------------------------------------------


def setup_cli_repo(repo: Path) -> Path:
    """Gives the temporary repository the real map and always-build list."""
    (repo / "scripts" / "affected").mkdir(parents=True)
    (repo / "scripts" / "affected" / "components.json").write_text(
        MAP_PATH.read_text(encoding="utf-8"), encoding="utf-8"
    )
    (repo / ".claude" / "tipoca" / "affected").mkdir(parents=True)
    (repo / ".claude" / "tipoca" / "affected" / "always-build.txt").write_text(
        ALWAYS_BUILD.read_text(encoding="utf-8"), encoding="utf-8"
    )
    git(repo, "add", ".")
    git(repo, "commit", "-q", "-m", "map")
    return repo


def cli(repo: Path, capsys: pytest.CaptureFixture[str], *args: str) -> tuple[int, str]:
    code = sel.main(
        ["--repo", str(repo), "--out-dir", str(repo.parent / "out"), "--no-dotnet", *args]
    )
    return code, capsys.readouterr().out


def test_cli_exits_166_when_nothing_changed(repo: Path, capsys: pytest.CaptureFixture[str]) -> None:
    setup_cli_repo(repo)
    code, out = cli(repo, capsys, "--from", "HEAD", "--format", "lines")
    assert code == sel.EXIT_NOTHING
    assert out.splitlines()[0] == "mode nothing"


def test_cli_lines_list_run_and_skip_per_group(
    repo: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    setup_cli_repo(repo)
    commit_file(repo, "worker/src/x.py")
    code, out = cli(repo, capsys, "--from", "HEAD~1", "--format", "lines")
    lines = out.splitlines()
    assert code == 0
    assert lines[0] == "mode affected"
    assert "group worker run worker/src/x.py" in lines
    assert "group generator skip no changed input" in lines
    assert "dotnet target StemStudio.slnx" in lines


def test_cli_github_marks_skipped_groups_false(
    repo: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    setup_cli_repo(repo)
    commit_file(repo, "worker/src/x.py")
    _, out = cli(repo, capsys, "--from", "HEAD~1", "--context", "ci", "--format", "github")
    assert "mode=affected" in out.splitlines()
    assert "worker=true" in out.splitlines()
    assert "generator=false" in out.splitlines()


def test_cli_full_reason_runs_everything(repo: Path, capsys: pytest.CaptureFixture[str]) -> None:
    setup_cli_repo(repo)
    code, out = cli(repo, capsys, "--full", "push to main", "--format", "json")
    data = json.loads(out)
    assert code == 0
    assert data["mode"] == "full"
    assert data["reason"] == "push to main"
    assert "generator" in data["groups"]


def test_cli_missing_base_is_full(repo: Path, capsys: pytest.CaptureFixture[str]) -> None:
    setup_cli_repo(repo)
    code, out = cli(
        repo, capsys, "--from", "0000000000000000000000000000000000000000", "--format", "text"
    )
    assert code == 0
    assert "full" in out
    assert "is not a commit" in out


def test_cli_broken_map_is_full(repo: Path, capsys: pytest.CaptureFixture[str]) -> None:
    (repo / "scripts" / "affected").mkdir(parents=True)
    (repo / "scripts" / "affected" / "components.json").write_text("{", encoding="utf-8")
    code, out = cli(repo, capsys, "--from", "HEAD", "--format", "github")
    assert code == 0
    assert out.splitlines()[0] == "mode=full"


def test_cli_text_report_names_reasons_and_hints(
    repo: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    setup_cli_repo(repo)
    commit_file(repo, "src/StemStudio.Agent/Tool.cs")
    _, out = cli(repo, capsys, "--from", "HEAD~1")
    assert "affected selection: affected" in out
    assert "run   dotnet" in out
    assert "skip  worker" in out
    assert "E2E tags probably touched (report only): @agent" in out


def failing_tool(*_: object) -> tuple[str, ...]:
    raise sel.SelectionError("dotnet-affected exit 3")


def with_dotnet(repo: Path, *args: str) -> int:
    """Runs the CLI with the .NET analysis on (dotnet_projects is monkeypatched by the caller)."""
    return sel.main(["--repo", str(repo), "--out-dir", str(repo.parent / "o"), *args])


def test_cli_dotnet_failure_falls_back_to_full(
    repo: Path, capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    setup_cli_repo(repo)
    commit_file(repo, "src/StemStudio.Domain/A.cs")
    monkeypatch.setattr(sel, "dotnet_projects", failing_tool)
    code = with_dotnet(repo, "--from", "HEAD~1", "--format", "lines")
    out = capsys.readouterr().out.splitlines()
    assert code == 0
    assert out[0] == "mode full"
    assert "reason analysis failed: dotnet-affected exit 3" in out


def test_cli_dotnet_projects_produce_a_filter(
    repo: Path, capsys: pytest.CaptureFixture[str], monkeypatch: pytest.MonkeyPatch
) -> None:
    setup_cli_repo(repo)
    commit_file(repo, "src/StemStudio.Domain/A.cs")
    monkeypatch.setattr(sel, "dotnet_projects", lambda *_: (DOMAIN_TESTS,))
    out_dir = repo.parent / "o"
    sel.main(
        ["--repo", str(repo), "--out-dir", str(out_dir), "--from", "HEAD~1", "--format", "lines"]
    )
    lines = capsys.readouterr().out.splitlines()
    assert f"dotnet target {(out_dir / 'affected.slnf').as_posix()}" in lines
    assert f"dotnet project {DOMAIN_TESTS}" in lines
    assert "group dotnet-perf skip no affected test project" in lines


def test_cli_rejects_unknown_options() -> None:
    with pytest.raises(SystemExit) as exit_info:
        sel.main(["--bogus"])
    assert exit_info.value.code == sel.EXIT_USAGE


# --- test-all and CI stay in step with the map --------------------------------------------------


SH_GROUP = re.compile(r"^group ([a-z0-9-]+)$", re.MULTILINE)
PS_GROUP = re.compile(r"^\s*@\{ Group = '([a-z0-9-]+)' \}$", re.MULTILINE)


def test_test_all_scripts_use_the_same_known_groups() -> None:
    cmap = sel.load_map(MAP_PATH)
    sh = SH_GROUP.findall((REPO_ROOT / "scripts" / "test-all.sh").read_text(encoding="utf-8"))
    ps = PS_GROUP.findall((REPO_ROOT / "scripts" / "test-all.ps1").read_text(encoding="utf-8"))
    assert sh == ps
    assert set(sh) <= set(cmap.groups)


SH_STEP = re.compile(r'^\s*step "([^"]+)" ([^ ]+) ', re.MULTILINE)


def test_a_change_in_a_step_folder_selects_that_step() -> None:
    """Every step that runs in a component folder runs when a file in that folder changes."""
    cmap = sel.load_map(MAP_PATH)
    text = (REPO_ROOT / "scripts" / "test-all.sh").read_text(encoding="utf-8")
    current = ""
    for line in text.splitlines():
        group = SH_GROUP.match(line)
        current = group.group(1) if group else current
        step = SH_STEP.match(line)
        if step and step.group(2) != ".":
            selected = sel.select_groups([f"{step.group(2)}/any.file"], cmap, ())
            assert current in selected.groups, step.group(1)


CI_GATE = re.compile(r"needs\.changes\.outputs\.([a-z0-9-]+) != 'false'")


def test_ci_gates_name_known_groups() -> None:
    cmap = sel.load_map(MAP_PATH)
    ci = (REPO_ROOT / ".github" / "workflows" / "ci.yml").read_text(encoding="utf-8")
    gates = set(CI_GATE.findall(ci))
    assert {"dotnet", "worker", "generator", "contracts", "licences"} <= gates
    assert gates <= set(cmap.groups)
