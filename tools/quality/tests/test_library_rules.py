"""Feature 022: library by artist and album, dependency direction (plan.md, tasks.md T024).

Rules (constitution II; the project rules of ``DOTNET_ALLOWED`` stay in force)::

    Domain/Library          depends on nothing outside Domain: no other StemStudio project, no
                            framework or package namespace (Microsoft.*, WeCantSpell), and not
                            on its sibling Domain/Lyrics (023)
    Api/Library             -> Domain, Persistence, Contracts (never Web or Agent, never the
                            sibling Api/LyricAssist)
    Web/Library, Web/Components/Library
                            -> Contracts only (no Api, Domain, Persistence, Agent), no direct
                            HttpClient (the API client is the one door) and not the sibling
                            Web/Components/Lyrics/Assist
    Agent/Tools             -> Agent.Model, Agent.Ports (and itself) only; no Runtime, Policy,
                            Backends or LyricAssist

The fixtures below are the violations the rules must find (written before the rules, T024 is test
first); the allowed fixtures must stay clean. Razor files count like C# files.
"""

from pathlib import Path

import pytest
from repo_tree import src_findings

import dependency_check
from common import REPO_ROOT, Severity
from dependency_check import DOTNET_FOLDER_ALLOWED, DOTNET_FOLDER_FORBIDDEN

DOMAIN = "StemStudio.Domain/Library"
API = "StemStudio.Api/Library"
WEB = "StemStudio.Web/Library"
WEB_UI = "StemStudio.Web/Components/Library"
TOOLS = "StemStudio.Agent/Tools"

HTTP = ("HttpClient", "IHttpClientFactory", "System.Net.Http")
FRAMEWORK = ("Microsoft", "WeCantSpell")
OTHER_PROJECTS = (
    "StemStudio.Agent",
    "StemStudio.Api",
    "StemStudio.Contracts",
    "StemStudio.Persistence",
    "StemStudio.Web",
)


def _message(folder: str, target: str) -> str:
    return f"forbidden dependency {folder} -> {target}"


# ------------------------------------------------------------------------------- the tables


def test_domain_library_depends_on_nothing_outside_domain() -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[DOMAIN]
    assert set(OTHER_PROJECTS) | set(FRAMEWORK) <= forbidden
    assert "StemStudio.Domain" not in forbidden
    assert "StemStudio.Domain.Lyrics" in forbidden


def test_api_library_reaches_domain_persistence_and_contracts_only() -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[API]
    assert {"StemStudio.Web", "StemStudio.Agent", "StemStudio.Api.LyricAssist"} <= forbidden
    assert not forbidden & {"StemStudio.Domain", "StemStudio.Persistence", "StemStudio.Contracts"}


@pytest.mark.parametrize("folder", [WEB, WEB_UI])
def test_web_library_reaches_contracts_only(folder: str) -> None:
    forbidden = DOTNET_FOLDER_FORBIDDEN[folder]
    assert {"StemStudio.Api", "StemStudio.Domain", "StemStudio.Persistence"} <= forbidden
    assert {"StemStudio.Agent", "StemStudio.Web.Components.Lyrics.Assist", *HTTP} <= forbidden
    assert not forbidden & {"StemStudio.Contracts", "StemStudio.Web", "StemStudio.Web.Api"}


def test_agent_tools_are_an_allow_list_of_model_and_ports() -> None:
    assert DOTNET_FOLDER_ALLOWED[TOOLS] == (
        "StemStudio.Agent",
        frozenset({"StemStudio.Agent.Model", "StemStudio.Agent.Ports"}),
    )
    assert TOOLS not in DOTNET_FOLDER_FORBIDDEN


# ------------------------------------------------------------------------------- Domain/Library


@pytest.mark.parametrize(
    ("rel", "text", "line", "target"),
    [
        ("Library/Filer.cs", "using StemStudio.Api.Library;\n", 1, "StemStudio.Api"),
        (
            "Library/Keys.cs",
            "using System;\n\nusing StemStudio.Persistence.Repositories;\n",
            3,
            "StemStudio.Persistence",
        ),
        ("Library/Dto.cs", "using StemStudio.Contracts.Library;\n", 1, "StemStudio.Contracts"),
        ("Library/Ui.cs", "using StemStudio.Web.Library;\n", 1, "StemStudio.Web"),
        ("Library/Agent.cs", "using StemStudio.Agent.Ports;\n", 1, "StemStudio.Agent"),
        (
            "Library/Lazy.cs",
            "class C\n{\n    StemStudio.Contracts.Library.FolderRefDto? _f;\n}\n",
            3,
            "StemStudio.Contracts",
        ),
        ("Library/Db.cs", "using Microsoft.EntityFrameworkCore;\n", 1, "Microsoft"),
        ("Library/Http.cs", "using Microsoft.AspNetCore.Http;\n", 1, "Microsoft"),
        (
            "Library/Log.cs",
            "class C\n{\n    Microsoft.Extensions.Logging.ILogger? _l;\n}\n",
            3,
            "Microsoft",
        ),
        ("Library/Spell.cs", "using WeCantSpell.Hunspell;\n", 1, "WeCantSpell"),
        (
            "Library/Sibling.cs",
            "using StemStudio.Domain.Lyrics.Assist;\n",
            1,
            "StemStudio.Domain.Lyrics",
        ),
    ],
)
def test_domain_library_forbidden_references(
    tmp_path: Path, rel: str, text: str, line: int, target: str
) -> None:
    expected = (Severity.ERROR, _message(DOMAIN, target), line, "backend")
    assert src_findings(tmp_path, f"StemStudio.Domain/{rel}", text) == [expected]


ALLOWED_DOMAIN = """namespace StemStudio.Domain.Library;

using System.Globalization;
using System.Text.Json.Serialization;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.LyricsHistory;

// A pure rule: never StemStudio.Api.Library, Microsoft.EntityFrameworkCore or HttpClient.
public sealed class PlacementResolver
{
    private const string Doc = "StemStudio.Persistence and Microsoft.Extensions";
    private readonly string _wecantspell = "WeCantSpell";
    public StemStudio.Domain.Assets.Asset? Asset { get; init; }
}
"""


def test_allowed_domain_library_is_clean(tmp_path: Path) -> None:
    assert src_findings(tmp_path, "StemStudio.Domain/Library/Placement.cs", ALLOWED_DOMAIN) == []


# ------------------------------------------------------------------------------- Api/Library


@pytest.mark.parametrize(
    ("text", "line", "target"),
    [
        ("using StemStudio.Web.Library;\n", 1, "StemStudio.Web"),
        ("using StemStudio.Agent.Ports;\n", 1, "StemStudio.Agent"),
        (
            "class C\n{\n    StemStudio.Agent.Ports.ILibraryActions? _a;\n}\n",
            3,
            "StemStudio.Agent",
        ),
        ("using StemStudio.Api.LyricAssist;\n", 1, "StemStudio.Api.LyricAssist"),
        ("using static StemStudio.Api.LyricAssist.Texts;\n", 1, "StemStudio.Api.LyricAssist"),
    ],
)
def test_api_library_forbidden_references(
    tmp_path: Path, text: str, line: int, target: str
) -> None:
    expected = (Severity.ERROR, _message(API, target), line, "backend")
    assert src_findings(tmp_path, "StemStudio.Api/Library/LibraryFiler.cs", text) == [expected]


ALLOWED_API = """namespace StemStudio.Api.Library;

using Microsoft.AspNetCore.Mvc;
using StemStudio.Api.Assets;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LyricAssistants;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Library;
using StemStudio.Persistence.Repositories;

public sealed class LibraryFiler
{
    private const string Note = "Never StemStudio.Web or StemStudio.Agent.Ports here";
}
"""


def test_allowed_api_library_is_clean(tmp_path: Path) -> None:
    assert src_findings(tmp_path, "StemStudio.Api/Library/LibraryFiler.cs", ALLOWED_API) == []


# ------------------------------------------------------------------------------- Web/Library


@pytest.mark.parametrize("folder", [WEB, WEB_UI])
@pytest.mark.parametrize(
    ("text", "line", "target"),
    [
        ("using StemStudio.Api.Library;\n", 1, "StemStudio.Api"),
        ("using StemStudio.Domain.Library;\n", 1, "StemStudio.Domain"),
        ("using StemStudio.Persistence;\n", 1, "StemStudio.Persistence"),
        ("using StemStudio.Agent.Ports;\n", 1, "StemStudio.Agent"),
        ("using System.Net.Http;\n", 1, "System.Net.Http"),
        ("using System.Net.Http.Json;\n", 1, "System.Net.Http"),
        ("class C\n{\n    private readonly HttpClient _http;\n}\n", 3, "HttpClient"),
        ("class C(IHttpClientFactory factory)\n{\n}\n", 1, "IHttpClientFactory"),
        (
            "using StemStudio.Web.Components.Lyrics.Assist;\n",
            1,
            "StemStudio.Web.Components.Lyrics.Assist",
        ),
    ],
)
def test_web_library_forbidden_references_in_csharp(
    tmp_path: Path, folder: str, text: str, line: int, target: str
) -> None:
    expected = (Severity.ERROR, _message(folder, target), line, "frontend")
    assert src_findings(tmp_path, f"{folder}/Holder.cs", text) == [expected]


@pytest.mark.parametrize(
    ("text", "line", "target"),
    [
        ("@using StemStudio.Domain.Library\n<h1>x</h1>\n", 1, "StemStudio.Domain"),
        ("<h1>x</h1>\n@inject StemStudio.Api.Library.LibraryIndex Index\n", 2, "StemStudio.Api"),
        ("@inject HttpClient Http\n", 1, "HttpClient"),
        ("@code {\n    private IHttpClientFactory? _f;\n}\n", 2, "IHttpClientFactory"),
        ("@using System.Net.Http\n", 1, "System.Net.Http"),
    ],
)
def test_web_library_forbidden_references_in_razor(
    tmp_path: Path, text: str, line: int, target: str
) -> None:
    expected = (Severity.ERROR, _message(WEB_UI, target), line, "frontend")
    assert src_findings(tmp_path, f"{WEB_UI}/FolderTile.razor", text) == [expected]


ALLOWED_RAZOR = """@using StemStudio.Contracts.Library
@using StemStudio.Web.Api
@using StemStudio.Web.Library
@using StemStudio.Web.Components.Lyrics.Songs
@* @using StemStudio.Domain.Library (comment) *@
@inject IStemStudioApiClient Client
<!-- StemStudio.Api.Library and HttpClient in a markup comment -->
<p title="StemStudio.Persistence">It's a folder, it's an album</p>
@code {
    // Never HttpClient or StemStudio.Agent.Ports here.
    private string Help => "System.Net.Http";
    private HttpClientLabel? _label;
}
"""


def test_allowed_razor_is_clean(tmp_path: Path) -> None:
    assert src_findings(tmp_path, f"{WEB_UI}/FolderTile.razor", ALLOWED_RAZOR) == []


def test_web_api_client_may_use_http(tmp_path: Path) -> None:
    text = "using System.Net.Http;\nclass C { HttpClient? _http; IHttpClientFactory? _f; }\n"
    assert src_findings(tmp_path, "StemStudio.Web/Api/StemStudioApiClient.Library.cs", text) == []


def test_razor_markup_apostrophes_do_not_hide_a_reference(tmp_path: Path) -> None:
    text = "<p>It's here</p>\n@using StemStudio.Api.Library\n<p>and it's gone</p>\n"
    expected = (Severity.ERROR, _message(WEB_UI, "StemStudio.Api"), 2, "frontend")
    assert src_findings(tmp_path, f"{WEB_UI}/Note.razor", text) == [expected]


# ------------------------------------------------------------------------------- Agent/Tools


@pytest.mark.parametrize(
    ("text", "line", "target"),
    [
        ("using StemStudio.Agent.Backends;\n", 1, "StemStudio.Agent.Backends"),
        ("using StemStudio.Agent.Runtime;\n", 1, "StemStudio.Agent.Runtime"),
        ("using StemStudio.Agent.Policy;\n", 1, "StemStudio.Agent.Policy"),
        ("using StemStudio.Agent.LyricAssist;\n", 1, "StemStudio.Agent.LyricAssist"),
        (
            "class T\n{\n    StemStudio.Agent.Runtime.RunLoop? _l;\n}\n",
            3,
            "StemStudio.Agent.Runtime.RunLoop",
        ),
        ("using StemStudio.Agent.ModelExtras;\n", 1, "StemStudio.Agent.ModelExtras"),
        ("using StemStudio.Agent;\n", 1, "StemStudio.Agent"),
    ],
)
@pytest.mark.parametrize("rel", ["Tools/Providers/LibraryToolProvider.cs", "Tools/Registry.cs"])
def test_agent_tools_reach_model_and_ports_only(
    tmp_path: Path, rel: str, text: str, line: int, target: str
) -> None:
    expected = (Severity.ERROR, _message(TOOLS, target), line, "ai-agent")
    assert src_findings(tmp_path, f"StemStudio.Agent/{rel}", text) == [expected]


ALLOWED_TOOLS = """namespace StemStudio.Agent.Tools.Providers;

using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Library;

// Never StemStudio.Agent.Runtime or StemStudio.Agent.Policy here.
sealed class LibraryToolProvider
{
    string Name = "StemStudio.Agent.Backends";
    StemStudio.Agent.Ports.ILibraryActions? _actions;
}
"""


def test_allowed_agent_tools_are_clean(tmp_path: Path) -> None:
    path = "StemStudio.Agent/Tools/Providers/LibraryToolProvider.cs"
    assert src_findings(tmp_path, path, ALLOWED_TOOLS) == []


# ------------------------------------------------------------------------------- the real tree


def test_real_library_sources_are_clean() -> None:
    folders = (DOMAIN, API, WEB, WEB_UI, TOOLS)
    findings = [f for f in dependency_check.run(REPO_ROOT) if any(d in f.message for d in folders)]
    assert findings == []
