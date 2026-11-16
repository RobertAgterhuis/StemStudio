"""Feature 011: composer agent rules (plan.md dependency direction, 011:T007).

Projects: ``Web -> (HTTP) -> Api -> Agent -> Contracts``. ``StemStudio.Agent`` references only
``StemStudio.Contracts`` (no Api, Domain, Persistence or Web) and only the Microsoft.Extensions.AI,
OllamaSharp and Anthropic packages; ``StemStudio.Web`` and ``StemStudio.Persistence`` never
reference ``StemStudio.Agent``; ``StemStudio.Api`` may.

Namespaces: only ``src/StemStudio.Agent/Backends/**`` uses the ``OllamaSharp`` and ``Anthropic``
namespaces; ``src/StemStudio.Agent/Policy/**`` is an allow-list (CTO decision, T080): inside
``StemStudio.Agent`` it references only ``Agent.Model``, ``Agent.Tools`` and the data-only
``Agent.Resources`` (plan.md: Policy references only Model and Tools; Resources holds the embedded
009 lexicon). Enum members, identifiers, comments and strings that merely contain the words are
not references.

Ownership: ``Ports/`` and ``Model/`` are the architect's, the rest of the library, its tests and
``tools/agent-eval/`` belong to ``ai-agent``.
"""

from pathlib import Path

import pytest
from repo_tree import write_file

import dependency_check
from common import DEFAULT_ROOTS, REPO_ROOT, Severity, owner_of
from components import COMPONENTS
from dependency_check import (
    DOTNET_ALLOWED,
    DOTNET_FOLDER_ALLOWED,
    DOTNET_FOLDER_EXEMPT,
    DOTNET_FOLDER_FORBIDDEN,
    DOTNET_PACKAGES_ALLOWED,
    check_packages,
    read_package_edges,
)

AGENT = "src/StemStudio.Agent"
CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
{items}
  </ItemGroup>
</Project>
"""


def _project(
    root: Path, name: str, refs: tuple[str, ...] = (), packages: tuple[str, ...] = ()
) -> None:
    items = [f'    <ProjectReference Include="..\\{r}\\{r}.csproj" />' for r in refs]
    items += [f'    <PackageReference Include="{p}" />' for p in packages]
    text = CSPROJ.format(items="\n".join(items))
    write_file(root, f"src/{name}/{name}.csproj", text)


def _messages(root: Path) -> list[tuple[Severity, str, str, int, str]]:
    return [(f.severity, f.message, f.path, f.line, f.owner) for f in dependency_check.run(root)]


def _agent_findings(root: Path, rel: str, text: str) -> list[tuple[str, int, str]]:
    write_file(root, f"{AGENT}/{rel}", text)
    return [(f.message, f.line, f.owner) for f in dependency_check.run(root)]


# ------------------------------------------------------------------------- project references


def test_agent_references_only_contracts() -> None:
    assert DOTNET_ALLOWED["StemStudio.Agent"] == frozenset({"StemStudio.Contracts"})


def test_api_may_reference_agent_but_web_and_persistence_may_not() -> None:
    assert "StemStudio.Agent" in DOTNET_ALLOWED["StemStudio.Api"]
    assert "StemStudio.Agent" not in DOTNET_ALLOWED["StemStudio.Web"]
    assert "StemStudio.Agent" not in DOTNET_ALLOWED["StemStudio.Persistence"]


CLEAN_GRAPH = {
    "StemStudio.Contracts": (),
    "StemStudio.Domain": (),
    "StemStudio.Persistence": ("StemStudio.Domain",),
    "StemStudio.Agent": ("StemStudio.Contracts",),
    "StemStudio.Api": ("StemStudio.Contracts", "StemStudio.Persistence", "StemStudio.Agent"),
    "StemStudio.Web": ("StemStudio.Contracts",),
}


def test_agent_graph_is_clean(tmp_path: Path) -> None:
    for name, refs in CLEAN_GRAPH.items():
        _project(tmp_path, name, refs, ("Microsoft.Extensions.AI", "OllamaSharp", "Anthropic"))
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    "target", ["StemStudio.Api", "StemStudio.Domain", "StemStudio.Persistence", "StemStudio.Web"]
)
def test_agent_may_not_reference_other_projects(tmp_path: Path, target: str) -> None:
    _project(tmp_path, "StemStudio.Agent", ("StemStudio.Contracts", target))
    path = f"{AGENT}/StemStudio.Agent.csproj"
    message = f"forbidden dependency StemStudio.Agent -> {target}"
    assert (Severity.ERROR, message, path, 0, "architect") in _messages(tmp_path)


@pytest.mark.parametrize(
    ("source", "owner"), [("StemStudio.Web", "frontend"), ("StemStudio.Persistence", "backend")]
)
def test_web_and_persistence_may_not_reference_agent(
    tmp_path: Path, source: str, owner: str
) -> None:
    _project(tmp_path, source, ("StemStudio.Agent",))
    path = f"src/{source}/{source}.csproj"
    message = f"forbidden dependency {source} -> StemStudio.Agent"
    assert _messages(tmp_path) == [(Severity.ERROR, message, path, 0, owner)]


# ------------------------------------------------------------------------- package references


def test_agent_package_allow_list() -> None:
    assert DOTNET_PACKAGES_ALLOWED["StemStudio.Agent"] == frozenset(
        {"Microsoft.Extensions.AI", "OllamaSharp", "Anthropic"}
    )


@pytest.mark.parametrize(
    "package",
    ["Microsoft.Extensions.AI", "Microsoft.Extensions.AI.Abstractions", "OllamaSharp", "Anthropic"],
)
def test_agent_allowed_packages(tmp_path: Path, package: str) -> None:
    _project(tmp_path, "StemStudio.Agent", ("StemStudio.Contracts",), (package,))
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    "package",
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions.AIX",
        "OllamaSharpExtras",
        "Microsoft.AspNetCore.OpenApi",
    ],
)
def test_agent_forbidden_packages(tmp_path: Path, package: str) -> None:
    _project(tmp_path, "StemStudio.Agent", ("StemStudio.Contracts",), ("OllamaSharp", package))
    message = f"forbidden package StemStudio.Agent -> {package}"
    path = f"{AGENT}/StemStudio.Agent.csproj"
    assert _messages(tmp_path) == [(Severity.ERROR, message, path, 0, "architect")]


def test_packages_of_unlisted_projects_are_not_checked(tmp_path: Path) -> None:
    _project(tmp_path, "StemStudio.Api", (), ("Microsoft.EntityFrameworkCore",))
    edges = read_package_edges(tmp_path / "src", tmp_path)
    assert [(e.source, e.target) for e in edges] == [
        ("StemStudio.Api", "Microsoft.EntityFrameworkCore")
    ]
    assert check_packages(edges, DOTNET_PACKAGES_ALLOWED) == []


def test_real_agent_project_packages_are_clean() -> None:
    edges = read_package_edges(REPO_ROOT / "src", REPO_ROOT)
    agent = {e.target for e in edges if e.source == "StemStudio.Agent"}
    assert agent == {"Microsoft.Extensions.AI", "OllamaSharp", "Anthropic"}
    assert check_packages(edges, DOTNET_PACKAGES_ALLOWED) == []


# ------------------------------------------------------------------------------- namespaces


def test_only_backends_is_exempt_from_the_sdk_namespaces() -> None:
    assert DOTNET_FOLDER_FORBIDDEN["StemStudio.Agent"] == frozenset({"OllamaSharp", "Anthropic"})
    assert DOTNET_FOLDER_EXEMPT["StemStudio.Agent"] == frozenset({"StemStudio.Agent/Backends"})


def test_policy_is_an_allow_list_of_model_tools_and_resources() -> None:
    allowed = {"StemStudio.Agent.Model", "StemStudio.Agent.Tools", "StemStudio.Agent.Resources"}
    assert DOTNET_FOLDER_ALLOWED["StemStudio.Agent/Policy"] == (
        "StemStudio.Agent",
        frozenset(allowed),
    )
    assert "StemStudio.Agent/Policy" not in DOTNET_FOLDER_FORBIDDEN


SDK = "forbidden dependency StemStudio.Agent -> "


@pytest.mark.parametrize(
    ("rel", "text", "expected"),
    [
        ("Runtime/RunLoop.cs", "using OllamaSharp;\n", (SDK + "OllamaSharp", 1, "ai-agent")),
        (
            "Runtime/RunLoop.cs",
            "using System;\nusing Anthropic.Models.Messages;\n",
            (SDK + "Anthropic", 2, "ai-agent"),
        ),
        ("Tools/Registry.cs", "global using Anthropic;\n", (SDK + "Anthropic", 1, "ai-agent")),
        ("Tools/Alias.cs", "using A = Anthropic.Client;\n", (SDK + "Anthropic", 1, "ai-agent")),
        (
            "Tools/Static.cs",
            "using static OllamaSharp.Models;\n",
            (SDK + "OllamaSharp", 1, "ai-agent"),
        ),
        (
            "Model/Kind.cs",
            "class K\n{\n    object c = new OllamaSharp.OllamaApiClient(u);\n}\n",
            (SDK + "OllamaSharp", 3, "architect"),
        ),
        (
            "Ports/Port.cs",
            "class P\n{\n    global::Anthropic.AnthropicClient? _c;\n}\n",
            (SDK + "Anthropic", 3, "architect"),
        ),
        (
            "AgentServiceCollectionExtensions.cs",
            "using OllamaSharp;\n",
            (SDK + "OllamaSharp", 1, "ai-agent"),
        ),
        ("Policy/Gate.cs", "using OllamaSharp;\n", (SDK + "OllamaSharp", 1, "ai-agent")),
    ],
)
def test_sdk_namespaces_outside_backends_are_forbidden(
    tmp_path: Path, rel: str, text: str, expected: tuple[str, int, str]
) -> None:
    assert _agent_findings(tmp_path, rel, text) == [expected]


POLICY = "forbidden dependency StemStudio.Agent/Policy -> "


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("using StemStudio.Agent.Runtime;\n", (POLICY + "StemStudio.Agent.Runtime", 1, "ai-agent")),
        (
            "using StemStudio.Agent.Backends.Fakes;\n",
            (POLICY + "StemStudio.Agent.Backends.Fakes", 1, "ai-agent"),
        ),
        (
            "class G\n{\n    StemStudio.Agent.Runtime.RunLoop? _l;\n}\n",
            (POLICY + "StemStudio.Agent.Runtime.RunLoop", 3, "ai-agent"),
        ),
        ("using StemStudio.Agent.Ports;\n", (POLICY + "StemStudio.Agent.Ports", 1, "ai-agent")),
        (
            "class G\n{\n    StemStudio.Agent.Ports.IRunStore? _s;\n}\n",
            (POLICY + "StemStudio.Agent.Ports.IRunStore", 3, "ai-agent"),
        ),
        ("using StemStudio.Agent;\n", (POLICY + "StemStudio.Agent", 1, "ai-agent")),
        (
            "using StemStudio.Agent.RuntimeOptions;\n",
            (POLICY + "StemStudio.Agent.RuntimeOptions", 1, "ai-agent"),
        ),
        (
            "using StemStudio.Agent.ModelExtras;\n",
            (POLICY + "StemStudio.Agent.ModelExtras", 1, "ai-agent"),
        ),
    ],
)
def test_policy_may_reference_nothing_else_of_the_agent(
    tmp_path: Path, text: str, expected: tuple[str, int, str]
) -> None:
    assert _agent_findings(tmp_path, "Policy/Gate.cs", text) == [expected]


ALLOWED_BACKEND = """namespace StemStudio.Agent.Backends;

using Anthropic;
using Anthropic.Models.Messages;
using OllamaSharp;

sealed class OllamaBackend
{
    OllamaSharp.OllamaApiClient? _client;
}
"""

ALLOWED_POLICY = """namespace StemStudio.Agent.Policy;

using StemStudio.Agent.Model;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Agent.Resources;
using StemStudio.Contracts.Agent;

// Never use OllamaSharp or Anthropic here; see "using OllamaSharp;" in Backends.
sealed class Gate
{
    string Name = "Anthropic.Models";
    string Runtime = "StemStudio.Agent.Runtime";
    StemStudio.Agent.Policy.ToolPolicy? Self;
    AgentBackendDto Kind = AgentBackendDto.Anthropic;
    string OllamaModel = "qwen";
    int AnthropicKeyLength;
}

enum Backend { Ollama, Anthropic }
"""


@pytest.mark.parametrize(
    ("rel", "text"),
    [
        ("Backends/OllamaBackend.cs", ALLOWED_BACKEND),
        ("Backends/Claude/ClaudeBackend.cs", ALLOWED_BACKEND),
        ("Policy/Gate.cs", ALLOWED_POLICY),
        ("Model/AgentSettings.cs", ALLOWED_POLICY),
    ],
)
def test_allowed_agent_files(tmp_path: Path, rel: str, text: str) -> None:
    assert _agent_findings(tmp_path, rel, text) == []


def test_real_agent_sources_are_clean() -> None:
    findings = [f for f in dependency_check.run(REPO_ROOT) if "Agent" in f.message]
    assert findings == []


# ------------------------------------------------------------------------- ownership, rows


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        ("src/StemStudio.Agent/Ports/IRunStore.cs", "architect"),
        ("src/StemStudio.Agent/Model/AgentRun.cs", "architect"),
        ("src/StemStudio.Agent/StemStudio.Agent.csproj", "architect"),
        ("src/StemStudio.Agent/Tools/ToolRegistry.cs", "ai-agent"),
        ("src/StemStudio.Agent/Policy/PolicyGate.cs", "ai-agent"),
        ("src/StemStudio.Agent/Runtime/RunLoop.cs", "ai-agent"),
        ("src/StemStudio.Agent/Backends/Fakes/ScriptedChatClient.cs", "ai-agent"),
        ("src/StemStudio.Agent/AgentServiceCollectionExtensions.cs", "ai-agent"),
        ("tests/StemStudio.Agent.Tests/PolicyGateTests.cs", "ai-agent"),
        ("tools/agent-eval/run_eval.py", "ai-agent"),
        ("src/StemStudio.Contracts/Agent/AgentRunDto.cs", "architect"),
    ],
)
def test_agent_owners(path: str, owner: str) -> None:
    assert owner_of(path) == owner


def test_agent_component_row() -> None:
    (row,) = [c for c in COMPONENTS if c.name == "Agent"]
    assert row.prefixes == ("src/StemStudio.Agent/",)
    assert row.contains("src/StemStudio.Agent/Policy/PolicyGate.cs", test=False)


def test_agent_eval_harness_is_measured() -> None:
    assert "tools/agent-eval" in DEFAULT_ROOTS
    (row,) = [c for c in COMPONENTS if c.name == "tools/agent-eval"]
    assert row.contains("tools/agent-eval/Program.cs", test=False)
    assert row.contains("tools/agent-eval/tests/GradingTests.cs", test=True)
