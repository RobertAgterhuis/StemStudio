"""Feature 023: lyric writing assistant, dependency direction (plan.md, tasks.md T071).

Direction ``Web -> API client -> API -> {Agent, Domain, Persistence}``; the project rules of
``DOTNET_ALLOWED`` stay in force (Agent -> Contracts only: the assistant gets its deterministic
checks through the Agent port ``ILyricVerification``, DELTA.md 5)::

    Domain (project)        no PackageReference at all
    Domain/Lyrics           (incl. Assist/) depends on nothing outside Domain: no other StemStudio
                            project, no framework or package namespace (Microsoft.*, WeCantSpell)
                            and not on its sibling Domain/Library (022)
    Api/LyricAssist         -> Agent, Domain, Persistence, Contracts (never Web, never the sibling
                            Api/Library); the Hunspell adapter lives here
    Web/Components/Lyrics/Assist
                            -> Contracts only; talks to the API through the API client (no
                            HttpClient, no WeCantSpell) and not to the sibling Web library UI
    Agent/LyricAssist       -> Agent.* and Contracts only (no Api, Domain, Persistence, Web, no
                            WeCantSpell)

Fixtures are the violations the rules must find; the allowed fixtures must stay clean.
"""

from pathlib import Path

import pytest
from repo_tree import src_findings, write_file

import dependency_check
from common import REPO_ROOT, Severity
from dependency_check import (
    DOTNET_FOLDER_FORBIDDEN,
    DOTNET_PACKAGES_ALLOWED,
    check_packages,
    read_package_edges,
)

DOMAIN = "StemStudio.Domain/Lyrics"
API = "StemStudio.Api/LyricAssist"
WEB = "StemStudio.Web/Components/Lyrics/Assist"
AGENT = "StemStudio.Agent/LyricAssist"

HTTP = ("HttpClient", "IHttpClientFactory", "System.Net.Http")


def _message(folder: str, target: str) -> str:
    return f"forbidden dependency {folder} -> {target}"


# ------------------------------------------------------------------------------- the tables


def test_domain_lyrics_depends_on_nothing_outside_domain() -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[DOMAIN]
    projects = {"StemStudio.Agent", "StemStudio.Api", "StemStudio.Contracts"}
    assert projects | {"StemStudio.Persistence", "StemStudio.Web"} <= forbidden
    assert {"Microsoft", "WeCantSpell", "StemStudio.Domain.Library"} <= forbidden
    assert "StemStudio.Domain" not in forbidden


def test_api_lyric_assist_may_use_agent_domain_persistence_and_contracts() -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[API]
    assert {"StemStudio.Web", "StemStudio.Api.Library"} <= forbidden
    assert not forbidden & {"StemStudio.Agent", "StemStudio.Domain", "StemStudio.Contracts"}
    assert "StemStudio.Persistence" not in forbidden


def test_web_assist_reaches_contracts_through_the_api_client_only() -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[WEB]
    assert {"StemStudio.Api", "StemStudio.Domain", "StemStudio.Persistence"} <= forbidden
    assert {"StemStudio.Agent", "WeCantSpell", *HTTP} <= forbidden
    assert {"StemStudio.Web.Library", "StemStudio.Web.Components.Library"} <= forbidden
    assert not forbidden & {"StemStudio.Contracts", "StemStudio.Web", "StemStudio.Web.Api"}


def test_agent_lyric_assist_reaches_contracts_only() -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[AGENT]
    assert {"StemStudio.Api", "StemStudio.Domain", "StemStudio.Persistence"} <= forbidden
    assert {"StemStudio.Web", "WeCantSpell"} <= forbidden
    assert not forbidden & {"StemStudio.Agent", "StemStudio.Contracts", "Microsoft"}


def test_domain_has_no_package_dependency() -> None:
    assert DOTNET_PACKAGES_ALLOWED["StemStudio.Domain"] == frozenset()


# ------------------------------------------------------------------------------- Domain package

CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
{items}
  </ItemGroup>
</Project>
"""


def _domain_project(root: Path, packages: tuple[str, ...]) -> None:
    items = "\n".join(f'    <PackageReference Include="{p}" />' for p in packages)
    write_file(root, "src/StemStudio.Domain/StemStudio.Domain.csproj", CSPROJ.format(items=items))


@pytest.mark.parametrize("package", ["WeCantSpell.Hunspell", "Microsoft.Extensions.Logging"])
def test_domain_packages_are_forbidden(tmp_path: Path, package: str) -> None:
    _domain_project(tmp_path, (package,))
    findings = [(f.severity, f.message, f.owner) for f in dependency_check.run(tmp_path)]
    message = f"forbidden package StemStudio.Domain -> {package}"
    assert findings == [(Severity.ERROR, message, "backend")]


def test_domain_without_packages_is_clean(tmp_path: Path) -> None:
    _domain_project(tmp_path, ())
    assert dependency_check.run(tmp_path) == []


def test_real_domain_has_no_package_reference() -> None:
    edges = read_package_edges(REPO_ROOT / "src", REPO_ROOT)
    assert [e.target for e in edges if e.source == "StemStudio.Domain"] == []
    assert check_packages(edges, DOTNET_PACKAGES_ALLOWED) == []


# ------------------------------------------------------------------------------- Domain/Lyrics


@pytest.mark.parametrize(
    ("rel", "text", "where"),
    [
        (
            "Lyrics/Assist/Hunspell.cs",
            "using WeCantSpell.Hunspell;\n",
            (1, "WeCantSpell"),
        ),
        (
            "Lyrics/Assist/Spell.cs",
            "using Microsoft.Extensions.Logging;\n",
            (1, "Microsoft"),
        ),
        (
            "Lyrics/LyricsSheet.cs",
            "using Microsoft.EntityFrameworkCore;\n",
            (1, "Microsoft"),
        ),
        (
            "Lyrics/Assist/Corpus.cs",
            "using StemStudio.Persistence.Repositories;\n",
            (1, "StemStudio.Persistence"),
        ),
        (
            "Lyrics/Assist/Dto.cs",
            "using StemStudio.Contracts.LyricAssist;\n",
            (1, "StemStudio.Contracts"),
        ),
        (
            "Lyrics/Assist/Verify.cs",
            "class V\n{\n    StemStudio.Agent.Ports.ILyricAssistActions? _a;\n}\n",
            (3, "StemStudio.Agent"),
        ),
        (
            "Lyrics/Assist/Service.cs",
            "using StemStudio.Api.LyricAssist;\n",
            (1, "StemStudio.Api"),
        ),
        (
            "Lyrics/Assist/Sibling.cs",
            "using StemStudio.Domain.Library;\n",
            (1, "StemStudio.Domain.Library"),
        ),
    ],
)
def test_domain_lyrics_forbidden_references(
    tmp_path: Path, rel: str, text: str, where: tuple[int, str]
) -> None:
    line, target = where
    expected = (Severity.ERROR, _message(DOMAIN, target), line, "backend")
    assert src_findings(tmp_path, f"StemStudio.Domain/{rel}", text) == [expected]


ALLOWED_DOMAIN = """namespace StemStudio.Domain.Lyrics.Assist;

using System.Globalization;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.LibraryRules;
using StemStudio.Domain.Tracks;

public interface ISpellChecker
{
    // Implemented by the Hunspell adapter (WeCantSpell) in StemStudio.Api.LyricAssist.
    string Engine => "WeCantSpell.Hunspell";
}
"""


def test_allowed_domain_lyrics_is_clean(tmp_path: Path) -> None:
    assert src_findings(tmp_path, "StemStudio.Domain/Lyrics/Assist/Ports.cs", ALLOWED_DOMAIN) == []


# ------------------------------------------------------------------------------- Api/LyricAssist


@pytest.mark.parametrize(
    ("text", "where"),
    [
        (
            "using StemStudio.Web.Components;\n",
            (1, "StemStudio.Web"),
        ),
        (
            "using StemStudio.Api.Library;\n",
            (1, "StemStudio.Api.Library"),
        ),
        (
            "class S\n{\n    StemStudio.Api.Library.LibraryIndex? _i;\n}\n",
            (3, "StemStudio.Api.Library"),
        ),
    ],
)
def test_api_lyric_assist_forbidden_references(
    tmp_path: Path, text: str, where: tuple[int, str]
) -> None:
    line, target = where
    expected = (Severity.ERROR, _message(API, target), line, "backend")
    assert src_findings(tmp_path, "StemStudio.Api/LyricAssist/Service.cs", text) == [expected]


ALLOWED_API = """namespace StemStudio.Api.LyricAssist;

using Microsoft.Extensions.Options;
using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.Ports;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;
using WeCantSpell.Hunspell;

public sealed class HunspellSpellChecker
{
    private const string Note = "Never StemStudio.Web or StemStudio.Api.Library here";
}
"""


def test_allowed_api_lyric_assist_is_clean(tmp_path: Path) -> None:
    path = "StemStudio.Api/LyricAssist/HunspellSpellChecker.cs"
    assert src_findings(tmp_path, path, ALLOWED_API) == []


# ------------------------------------------------------------------------------- Web assist


@pytest.mark.parametrize(
    ("text", "where"),
    [
        (
            "using StemStudio.Api.LyricAssist;\n",
            (1, "StemStudio.Api"),
        ),
        (
            "using StemStudio.Domain.Lyrics.Assist;\n",
            (1, "StemStudio.Domain"),
        ),
        (
            "using StemStudio.Persistence;\n",
            (1, "StemStudio.Persistence"),
        ),
        (
            "using StemStudio.Agent.Ports;\n",
            (1, "StemStudio.Agent"),
        ),
        (
            "using WeCantSpell.Hunspell;\n",
            (1, "WeCantSpell"),
        ),
        (
            "using System.Net.Http;\n",
            (1, "System.Net.Http"),
        ),
        (
            "class P\n{\n    private readonly HttpClient _http;\n}\n",
            (3, "HttpClient"),
        ),
        (
            "class P(IHttpClientFactory factory)\n{\n}\n",
            (1, "IHttpClientFactory"),
        ),
        (
            "using StemStudio.Web.Library;\n",
            (1, "StemStudio.Web.Library"),
        ),
        (
            "using StemStudio.Web.Components.Library;\n",
            (1, "StemStudio.Web.Components.Library"),
        ),
    ],
)
def test_web_assist_forbidden_references_in_csharp(
    tmp_path: Path, text: str, where: tuple[int, str]
) -> None:
    line, target = where
    expected = (Severity.ERROR, _message(WEB, target), line, "frontend")
    assert src_findings(tmp_path, f"{WEB}/Pane.cs", text) == [expected]


@pytest.mark.parametrize(
    ("text", "where"),
    [
        (
            "@using StemStudio.Domain.Lyrics.Assist\n<div></div>\n",
            (1, "StemStudio.Domain"),
        ),
        (
            "@inject HttpClient Http\n",
            (1, "HttpClient"),
        ),
        (
            "<div></div>\n@using StemStudio.Web.Library\n",
            (2, "StemStudio.Web.Library"),
        ),
    ],
)
def test_web_assist_forbidden_references_in_razor(
    tmp_path: Path, text: str, where: tuple[int, str]
) -> None:
    line, target = where
    expected = (Severity.ERROR, _message(WEB, target), line, "frontend")
    assert src_findings(tmp_path, f"{WEB}/AssistPane.razor", text) == [expected]


ALLOWED_PANE = """@using StemStudio.Contracts.LyricAssist
@using StemStudio.Web.Api
@using StemStudio.Web.Songs
@inject IStemStudioApiClient Client
@* StemStudio.Domain.Lyrics.Assist is only mentioned here *@
<p>The assistant's suggestions, the writer's choice</p>
@code {
    // No HttpClient: the API client is the door.
    private string Hint => "StemStudio.Api.LyricAssist";
}
"""


def test_allowed_web_assist_is_clean(tmp_path: Path) -> None:
    assert src_findings(tmp_path, f"{WEB}/AssistPane.razor", ALLOWED_PANE) == []


# ------------------------------------------------------------------------------- Agent/LyricAssist


@pytest.mark.parametrize(
    ("text", "where"),
    [
        (
            "using StemStudio.Domain.Lyrics.Assist;\n",
            (1, "StemStudio.Domain"),
        ),
        (
            "using StemStudio.Api.LyricAssist;\n",
            (1, "StemStudio.Api"),
        ),
        (
            "using StemStudio.Persistence;\n",
            (1, "StemStudio.Persistence"),
        ),
        (
            "using StemStudio.Web.Api;\n",
            (1, "StemStudio.Web"),
        ),
        (
            "using WeCantSpell.Hunspell;\n",
            (1, "WeCantSpell"),
        ),
        (
            "class A\n{\n    StemStudio.Domain.Lyrics.Assist.AssistLanguage _l;\n}\n",
            (3, "StemStudio.Domain"),
        ),
    ],
)
def test_agent_lyric_assist_forbidden_references(
    tmp_path: Path, text: str, where: tuple[int, str]
) -> None:
    line, target = where
    expected = (Severity.ERROR, _message(AGENT, target), line, "ai-agent")
    assert src_findings(tmp_path, f"{AGENT}/LyricAssistant.cs", text) == [expected]


ALLOWED_AGENT = """namespace StemStudio.Agent.LyricAssist;

using Microsoft.Extensions.AI;
using StemStudio.Agent.Backends;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Contracts.LyricAssist;

// ILyricVerification replaces the Domain services (DELTA.md 5): never StemStudio.Domain here.
sealed class LyricAssistant
{
    string Why = "StemStudio.Domain.Lyrics.Assist";
}
"""


def test_allowed_agent_lyric_assist_is_clean(tmp_path: Path) -> None:
    assert src_findings(tmp_path, f"{AGENT}/LyricAssistant.cs", ALLOWED_AGENT) == []


# ------------------------------------------------------------------------------- the real tree


def test_real_lyric_assist_sources_are_clean() -> None:
    folders = (DOMAIN, API, WEB, AGENT, "StemStudio.Domain ->")
    findings = [f for f in dependency_check.run(REPO_ROOT) if any(d in f.message for d in folders)]
    assert findings == []
