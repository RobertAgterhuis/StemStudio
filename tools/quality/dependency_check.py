"""Dependency-direction check for the .NET projects and the Python worker (plan.md).

.NET (``<ProjectReference>`` in ``src/*/*.csproj``)::

    Web -> Contracts;  Api -> {Contracts, Domain, Persistence, Agent};  Persistence -> Domain
    Agent -> Contracts (feature 011);  Domain and Contracts reference no StemStudio project.

.NET packages (``<PackageReference>``, only for the projects in ``DOTNET_PACKAGES_ALLOWED``)::

    Agent -> {Microsoft.Extensions.AI[.*], OllamaSharp[.*], Anthropic[.*]}   (feature 011)
    Domain -> no package at all                                               (feature 023 T071)

.NET feature folders (``using`` directives and qualified names in ``src/<folder>/**/*.cs``;
comments, string/char literals and ``namespace`` declarations are ignored)::

    Api/Metadata, Api/Artwork -/- Web            (feature 005)
    Api/Assets -/- Web;  Domain/Rendering -/- {Api, Persistence, Web, Contracts}   (feature 007)
    Api/Generation, Api/Generator -/- Web                                          (feature 009)
    Api/Voices -/- {Web, Api.Generation};  Api/Generator -/- {Api.Generation, Api.Voices}  (010)
    Agent (except Agent/Backends) -/- {OllamaSharp, Anthropic}                      (feature 011)
    Domain/Library, Domain/Lyrics -/- every other StemStudio project, Microsoft.*, WeCantSpell
        and each other                                                      (022 T024, 023 T071)
    Api/Library -/- {Web, Agent, Api.LyricAssist};  Api/LyricAssist -/- {Web, Api.Library}
    Web/Library, Web/Components/Library, Web/Components/Lyrics/Assist -> Contracts and Web.* only,
        no HttpClient (the API client in Web/Api is the one door) and not each other's UI
    Agent/LyricAssist -/- {Api, Domain, Persistence, Web, WeCantSpell}   (Agent -> Contracts only)
    Domain/{Sections, Setlists, Practice} -/- {Api, Persistence, Web}             (014 T015)
    Api/Assets, Api/Tracks -/- Api.Recording (only their own IAssetOriginPolicy /
        ITrackTakesCleanup; Recording implements them)                            (015 T007)

.NET feature-folder allow-lists (same references; only names below the scope root count)::

    Agent/Policy -> StemStudio.Agent.{Model, Tools, Resources} only    (feature 011, CTO T080;
                    plan.md: Model and Tools; Resources = the embedded 009 lexicon, data only)
    Agent/Tools  -> StemStudio.Agent.{Model, Ports} only               (feature 022 T024: the tools
                    reach the application through the ports, never Runtime, Policy or Backends)

    Razor files (``*.razor``) of a rule folder count like C# files (``@using``, ``@inject``,
    ``@code``; ``@* *@`` and ``<!-- -->`` comments are ignored).

    The SDK namespaces count in ``using`` directives (incl. ``global``, ``static``, aliases) and
    in qualified names (``OllamaSharp.OllamaApiClient``); a member access (``Dto.Anthropic``),
    an enum member or an identifier (``OllamaModel``) is not a reference.

Worker (imports inside ``stemstudio_worker``)::

    api -> jobs -> ports <- {ingest, separation, analysis, detail, devices, metadata,
    render, lyrics, alignment, transcription};  notation, engraving -> ports
    everything -> schemas, config (notation and engraving: schemas only)
    lyrics may also import metadata.{http, ratelimit, user_agent} (feature 006: shared egress),
    no other metadata module; nothing imports lyrics or alignment (feature 006: jobs gets the
    LyricsLookup and the Aligner injected); nothing imports transcription, notation or
    engraving (feature 008: composed in main.py); transcription/vendor/** contributes no edges
    storage -> {jobs, ports, schemas, config} only (feature 013: never separation, detail or
    api); api may import storage (the storage routes); jobs and the adapters never do

Worker third-party imports (feature 008; the standard library is always allowed)::

    notation -> music21 only;  engraving -> verovio, cairosvg, pypdf only
    detail may also import its own vendor package (detail/vendor/** is third-party code and
    contributes no edges); no module imports devices (feature 003: separation and detail get
    the DeviceRunner injected); no module imports metadata (feature 005: ingest gets the
    ImageNormalizer injected); no module imports render (feature 007: jobs gets the
    RenderExecutor injected); main.py is the composition root and may import every module.
    documents -> ports, schemas and (third party) reportlab only (feature 014)

Worker scopes (feature 014 T015, feature 016 T009; file level, stricter than their module)::

    harmony/{postprocess,labels,chroma_hmm,key}.py -> ports, schemas, each other and numpy,
        librosa only;  harmony/btc.py -> ports, schemas, harmony.vendor and numpy, librosa,
        torch only;  harmony/vendor/** is third-party code (no edges);  views_fake -> ports,
        schemas, config and jobs.instrument_analysis_ports, lyrics.{fake, interpolate},
        transcription.{fake, audio} only;  nothing but main.py imports harmony or views_fake

    analysis/structure/** -> ports, schemas, analysis.structure and numpy, scipy, sklearn,
        librosa, soundfile only;  engraving/score_view.py -> ports, schemas, engraving and
        verovio only (stdlib xml for the SVG);  jobs and api import none of them nor documents

Generator (imports inside ``stemstudio_generator``, features 009/010)::

    api -> jobs -> ports <- {engines, devices, audio};  everything -> schemas, config
    engines, devices and audio import only ports, schemas, config; nothing but main.py imports
    an adapter (main.py composes); ports, schemas, config are leaves

Worker and generator never import each other (separate services; they share only the GPU lease
file): ``stemstudio_worker -/- stemstudio_generator`` in both directions, ``main.py`` included.

TypeScript packages (``src/<name>/package.json``, imports in their ``*.ts`` files)::

    web-interop -/- audio-engine   (feature 004: neither package imports the other)
    audio-engine/{transport, mixer} -/- audio-engine/karaoke   (feature 006, module level)
    audio-engine/{transport, mixer, metronome, karaoke} -/- audio-engine/{practice, stretch}
    audio-engine/practice -/- audio-engine/karaoke                (feature 012, module level)
    audio-engine/score-follow -/- audio-engine/{callbacks, index}  (014: no Blazor interop module)
    audio-engine/recording <- audio-engine/engine only            (015 T007: importer allow-list)

Worklet scope (feature 012, file level inside ``src/audio-engine/src/stretch/``)::

    stretch/processor.ts -> stretch/{constants, params, fifo, abi, core} only (any import)
    stretch/{constants, params, fifo, abi, core} value-import only each other

    The AudioWorklet bundle ``time-stretch-processor.js`` is built from ``processor.ts`` and
    must hold no DOM or engine code. ``import type``/``export type`` statements are erased by the
    build, so the non-entry worklet modules may use them (``params`` reads ``PracticeSettings``).

    An import is an edge when its specifier is ``@stemstudio/<name>[/...]`` or a relative path
    that leaves the package into ``src/<name>/``; npm packages (``driver.js``) are not edges.
    A module edge ``<package>/<module>`` is a relative import between the top-level files or
    folders of ``src/<package>/src/`` (``transport.ts`` and ``transport/**`` are ``transport``).

Forbidden edges and cycles fail the gate; modules missing from the map are warnings.

Usage::

    python tools/quality/dependency_check.py [--root DIR]

Exit code 0 = clean, 1 = forbidden edge or cycle.
"""

import ast
import re
import sys

# Project files are trusted repository content; Python 3.12's expat does not expand external
# entities and guards against entity-expansion attacks, so defusedxml is not needed.
import xml.etree.ElementTree as ET
from collections.abc import Iterator, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path, PureWindowsPath

from common import (
    EXCLUDED_DIRS,
    Finding,
    Severity,
    build_parser,
    is_excluded,
    relative,
    report_findings,
)

DOTNET_ALLOWED: Mapping[str, frozenset[str]] = {
    "StemStudio.Domain": frozenset(),
    "StemStudio.Contracts": frozenset(),
    "StemStudio.Persistence": frozenset({"StemStudio.Domain"}),
    "StemStudio.Api": frozenset(
        {"StemStudio.Domain", "StemStudio.Persistence", "StemStudio.Contracts", "StemStudio.Agent"}
    ),
    "StemStudio.Web": frozenset({"StemStudio.Contracts"}),
    "StemStudio.Agent": frozenset({"StemStudio.Contracts"}),
}
"""Allowed project references per production project."""

DOTNET_PACKAGES_ALLOWED: Mapping[str, frozenset[str]] = {
    "StemStudio.Agent": frozenset({"Microsoft.Extensions.AI", "OllamaSharp", "Anthropic"}),
    "StemStudio.Domain": frozenset(),
}
"""Allowed NuGet packages per project; a name ``a.b`` also allows ``a.b.<anything>``.

Projects missing here are not restricted.
"""

DOTNET_PROJECT_NAMES = ("Agent", "Api", "Contracts", "Domain", "Persistence", "Web")
_DOTNET_PROJECTS = "|".join(DOTNET_PROJECT_NAMES)

DOTNET_SDK_NAMESPACES = ("OllamaSharp", "Anthropic")
"""Model SDK root namespaces only ``Agent/Backends`` may use (feature 011)."""

DOTNET_FRAMEWORK_NAMESPACES = ("Microsoft", "WeCantSpell")
"""Framework and package root namespaces pure Domain code may not use (022 T024, 023 T071)."""

DOTNET_HTTP_NAMES = ("HttpClient", "IHttpClientFactory", "System.Net.Http")
"""Direct HTTP use the Web UI folders leave to the API client (``Web/Api``, 023 T071)."""

DOTNET_EXTERNAL_NAMESPACES = (*DOTNET_SDK_NAMESPACES, *DOTNET_FRAMEWORK_NAMESPACES)
"""Third-party root namespaces a feature-folder rule may forbid (features 011, 022, 023)."""


def _projects_except(*allowed: str) -> frozenset[str]:
    """The ``StemStudio.<Project>`` namespaces of every project not in ``allowed``."""
    return frozenset(f"StemStudio.{p}" for p in DOTNET_PROJECT_NAMES if p not in allowed)


def _pure_domain(sibling: str) -> frozenset[str]:
    """Nothing outside Domain: other projects, framework and package namespaces, the sibling."""
    return _projects_except("Domain") | set(DOTNET_FRAMEWORK_NAMESPACES) | {sibling}


def _web_ui(*siblings: str) -> frozenset[str]:
    """A Web UI folder: Contracts only (through the API client, so no HTTP), no sibling UI."""
    others = _projects_except("Contracts", "Web") | set(DOTNET_HTTP_NAMES) | {"WeCantSpell"}
    return others | set(siblings)


_PRACTICE_DOMAIN = frozenset({"StemStudio.Api", "StemStudio.Persistence", "StemStudio.Web"})
"""Feature 014: the practice domain folders reference no outer layer."""

DOTNET_FOLDER_FORBIDDEN: Mapping[str, frozenset[str]] = {
    "StemStudio.Api/Metadata": frozenset({"StemStudio.Web"}),
    "StemStudio.Api/Artwork": frozenset({"StemStudio.Web"}),
    "StemStudio.Api/Assets": frozenset({"StemStudio.Web", "StemStudio.Api.Recording"}),
    "StemStudio.Api/Tracks": frozenset({"StemStudio.Web", "StemStudio.Api.Recording"}),
    "StemStudio.Domain/Rendering": frozenset(
        {"StemStudio.Api", "StemStudio.Persistence", "StemStudio.Web", "StemStudio.Contracts"}
    ),
    "StemStudio.Api/Generation": frozenset({"StemStudio.Web"}),
    "StemStudio.Api/Generator": frozenset(
        {"StemStudio.Web", "StemStudio.Api.Generation", "StemStudio.Api.Voices"}
    ),
    "StemStudio.Api/Voices": frozenset({"StemStudio.Web", "StemStudio.Api.Generation"}),
    "StemStudio.Agent": frozenset(DOTNET_SDK_NAMESPACES),
    "StemStudio.Domain/Library": _pure_domain("StemStudio.Domain.Lyrics"),
    "StemStudio.Domain/Lyrics": _pure_domain("StemStudio.Domain.Library"),
    "StemStudio.Api/Library": frozenset(
        {"StemStudio.Web", "StemStudio.Agent", "StemStudio.Api.LyricAssist"}
    ),
    "StemStudio.Api/LyricAssist": frozenset({"StemStudio.Web", "StemStudio.Api.Library"}),
    "StemStudio.Web/Library": _web_ui("StemStudio.Web.Components.Lyrics.Assist"),
    "StemStudio.Web/Components/Library": _web_ui("StemStudio.Web.Components.Lyrics.Assist"),
    "StemStudio.Web/Components/Lyrics/Assist": _web_ui(
        "StemStudio.Web.Library", "StemStudio.Web.Components.Library"
    ),
    "StemStudio.Agent/LyricAssist": _projects_except("Agent", "Contracts") | {"WeCantSpell"},
    "StemStudio.Domain/Sections": _PRACTICE_DOMAIN,
    "StemStudio.Domain/Setlists": _PRACTICE_DOMAIN,
    "StemStudio.Domain/Practice": _PRACTICE_DOMAIN,
}
"""Forbidden StemStudio namespaces per feature folder below ``src/`` (all files below it).

An entry forbids the namespace and every namespace below it (``StemStudio.Web`` forbids
``StemStudio.Web.Pages``, ``StemStudio.Api.Generation`` does not forbid
``StemStudio.Api.GenerationTools``).
"""

DOTNET_FOLDER_ALLOWED: Mapping[str, tuple[str, frozenset[str]]] = {
    "StemStudio.Agent/Policy": (
        "StemStudio.Agent",
        frozenset(
            {"StemStudio.Agent.Model", "StemStudio.Agent.Tools", "StemStudio.Agent.Resources"}
        ),
    ),
    "StemStudio.Agent/Tools": (
        "StemStudio.Agent",
        frozenset({"StemStudio.Agent.Model", "StemStudio.Agent.Ports"}),
    ),
}
"""Allow-lists per feature folder below ``src/``: ``folder -> (scope root, allowed names)``.

A reference to the scope root or below it is an error unless it is (below) an allowed name or
the folder's own namespace (``StemStudio.Agent.Policy``); names outside the scope root
(``StemStudio.Contracts``, ``System``) are left to the project rules. Families are matched per
dotted segment (``StemStudio.Agent.Model`` does not allow ``StemStudio.Agent.ModelExtras``).
"""

DOTNET_FOLDER_EXEMPT: Mapping[str, frozenset[str]] = {
    "StemStudio.Agent": frozenset({"StemStudio.Agent/Backends"}),
}
"""Sub-folders (relative to ``src/``) whose files a folder rule does not check."""

_CS_NOISE = re.compile(
    "|".join(
        (
            r'"""[\s\S]*?"""',  # raw string literal
            r'(?:\$@|@\$?)"(?:""|[^"])*"',  # verbatim (interpolated) string
            r'"(?:\\.|[^"\\\n])*"',  # regular or interpolated string
            r"'(?:\\.|[^'\\\n])+'",  # char literal
            r"//[^\n]*",  # line comment
            r"/\*[\s\S]*?\*/",  # block comment
            r"@\*[\s\S]*?\*@",  # Razor comment
            r"<!--[\s\S]*?-->",  # markup comment
            r"^[ \t]*namespace\b[^\n]*",  # namespace declaration
        )
    ),
    re.MULTILINE,
)
"""C#/Razor comments, string/char literals and namespace declarations (no references)."""

_CS_REFERENCE = re.compile(rf"\bStemStudio\.(?:{_DOTNET_PROJECTS})\b(?:\.[A-Za-z_]\w*)*")
"""A qualified reference into a StemStudio project namespace (the whole dotted name)."""

_EXTERNAL = "|".join(DOTNET_EXTERNAL_NAMESPACES)
_CS_EXTERNAL_QUALIFIED = re.compile(rf"(?<![\w.])(?P<name>{_EXTERNAL})(?:\.[A-Za-z_]\w*)+")
_CS_EXTERNAL_USING = re.compile(
    rf"^[ \t]*(?:global[ \t]+)?using[ \t]+(?:static[ \t]+)?(?:\w+[ \t]*=[ \t]*)?"
    rf"(?:global::)?(?P<name>{_EXTERNAL})[ \t]*;",
    re.MULTILINE,
)
"""A qualified third-party name, or a ``using`` directive of a bare third-party root namespace;
the edge target is the root namespace (``OllamaSharp``)."""

_CS_HTTP_TYPE = re.compile(r"(?<![\w.])(?P<name>HttpClient|IHttpClientFactory)\b")
_CS_HTTP_NAMESPACE = re.compile(r"(?<![\w.])(?P<name>System\.Net\.Http)\b")
"""Direct HTTP use: the edge target is the type name or ``System.Net.Http`` (also for a name
below it), at an identifier of its own (``HttpClientLabel`` and ``Dto.HttpClient`` are not)."""

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
    "metadata": _ADAPTER,
    "render": _ADAPTER,
    "lyrics": _ADAPTER,
    "alignment": _ADAPTER,
    "transcription": _ADAPTER,
    "notation": frozenset({"ports", "schemas"}),
    "engraving": frozenset({"ports", "schemas"}),
    "documents": frozenset({"ports", "schemas"}),
    "harmony": _ADAPTER,
    "analysis_inputs": _ADAPTER,
    "views_fake": _ADAPTER,
    "devices": _ADAPTER,
    "jobs": _ADAPTER,
    "storage": _ADAPTER | {"jobs"},
    "api": _ADAPTER | {"jobs", "storage"},
    "main": None,
}
"""Allowed imported top-level modules per worker module; ``None`` = unrestricted."""

PYTHON_SUBMODULES_ALLOWED: Mapping[str, frozenset[str]] = {
    "lyrics": frozenset({"metadata.http", "metadata.ratelimit", "metadata.user_agent"}),
    "views_fake": frozenset({
        "jobs.instrument_analysis_ports", "lyrics.fake", "lyrics.interpolate",
        "transcription.fake", "transcription.audio",
    }),
}  # fmt: skip
"""Modules (dotted, below the package) a worker module may import beyond ``PYTHON_ALLOWED``.

``metadata.http`` also allows ``metadata.http.<name>``; ``metadata.httpx`` stays forbidden.
Feature 016: the fake analyses (``views_fake``) reuse the ``ChordAnalyzer`` port types, the 006
fake lyric lines and the 008 fake drum groove, nothing else of those modules.
"""

PYTHON_EXTERNAL_ALLOWED: Mapping[str, frozenset[str]] = {
    "notation": frozenset({"music21"}),
    "engraving": frozenset({"verovio", "cairosvg", "pypdf"}),
    "documents": frozenset({"reportlab"}),
}
"""Third-party top-level packages a worker module may import (only modules listed here are
restricted; the standard library and ``stemstudio_worker`` itself are never third party)."""


@dataclass(frozen=True, slots=True)
class PythonScope:
    """Imports allowed in the files of one worker scope (stricter than its top-level module).

    ``internal`` are dotted families below the package (``analysis.structure`` allows
    ``analysis.structure.names``); ``external`` third-party top-level names. The standard library
    is always allowed.
    """

    internal: frozenset[str]
    external: frozenset[str]


_HARMONY_CORE = PythonScope(
    frozenset({"ports", "schemas", "harmony.postprocess", "harmony.labels", "harmony.chroma_hmm",
               "harmony.key"}),
    frozenset({"numpy", "librosa"}),
)  # fmt: skip
"""Feature 016: labels, post-processing, chroma-hmm and key import only ports, schemas, each other
and numpy, librosa (never torch, the BTC adapter or its vendor)."""

PYTHON_SCOPES: Mapping[str, PythonScope] = {
    "analysis/structure/": PythonScope(
        frozenset({"ports", "schemas", "analysis.structure"}),
        frozenset({"numpy", "scipy", "sklearn", "librosa", "soundfile"}),
    ),
    "engraving/score_view.py": PythonScope(
        frozenset({"ports", "schemas", "engraving"}), frozenset({"verovio"})
    ),
    **{
        f"harmony/{name}.py": _HARMONY_CORE
        for name in ("postprocess", "labels", "chroma_hmm", "key")
    },
    "harmony/btc.py": PythonScope(
        frozenset({"ports", "schemas", "harmony.vendor"}),
        frozenset({"numpy", "librosa", "torch"}),
    ),
}
"""Worker scopes by package-relative path (a folder ends with ``/``): feature 014 (T015) and
feature 016 (T009: the harmony core and the BTC adapter)."""

STDLIB = frozenset(sys.stdlib_module_names) | {"__future__"}
"""Top-level names of the standard library."""

PYTHON_PACKAGE = "stemstudio_worker"
"""Name of the worker package."""

GENERATOR_ALLOWED: Mapping[str, frozenset[str] | None] = {
    "__init__": _LEAVES,
    "schemas": _LEAVES,
    "config": _LEAVES,
    "ports": _LEAVES,
    "engines": _ADAPTER,
    "devices": _ADAPTER,
    "audio": _ADAPTER,
    "jobs": _ADAPTER,
    "api": _ADAPTER | {"jobs"},
    "main": None,
}
"""Allowed imported top-level modules per generator module; ``None`` = unrestricted."""

GENERATOR_PACKAGE = "stemstudio_generator"
"""Name of the generator package (features 009/010)."""


@dataclass(frozen=True, slots=True)
class PythonPackage:
    """A Python service package checked by its own layer map.

    ``path`` is repository-relative; ``foreign`` is the other service's package, which no module
    of this package may import.
    """

    name: str
    path: str
    allowed: Mapping[str, frozenset[str] | None]
    foreign: str


WORKER = PythonPackage(
    PYTHON_PACKAGE, "worker/src/stemstudio_worker", PYTHON_ALLOWED, GENERATOR_PACKAGE
)
GENERATOR = PythonPackage(
    GENERATOR_PACKAGE, "generator/src/stemstudio_generator", GENERATOR_ALLOWED, PYTHON_PACKAGE
)
PYTHON_PACKAGES = (WORKER, GENERATOR)
"""The Python service packages and their layer maps."""

TS_ALLOWED: Mapping[str, frozenset[str] | None] = {
    "web-interop": frozenset(),
    "audio-engine": frozenset(),
}
"""Allowed sibling packages per TypeScript package below ``src/`` (none: they are independent)."""

TS_DATA_IMPORTS_ALLOWED: Mapping[str, frozenset[str]] = {
    "web-interop": frozenset({"StemStudio.Web/wwwroot/js/keymap-defaults.json"}),
}
"""Data files (paths below ``src/``) a TypeScript package may import across packages.

Feature 014 (web-interop 1.1.0 sections 5.1/5.4): ``defaultKeyMap`` reads the key map catalogue
at build time, the same JSON file ``KeyMapDefaults`` reads in C#. Data, not code: no edge.
"""

_PRACTICE = frozenset({"audio-engine/practice", "audio-engine/stretch"})

TS_MODULE_FORBIDDEN: Mapping[str, frozenset[str]] = {
    "audio-engine/transport": frozenset({"audio-engine/karaoke"}) | _PRACTICE,
    "audio-engine/mixer": frozenset({"audio-engine/karaoke"}) | _PRACTICE,
    "audio-engine/metronome": _PRACTICE,
    "audio-engine/karaoke": _PRACTICE,
    "audio-engine/practice": frozenset({"audio-engine/karaoke"}),
    "audio-engine/score-follow": frozenset({"audio-engine/callbacks", "audio-engine/index"}),
}
"""Forbidden module edges inside a TypeScript package (``<package>/<top-level module>``).

Feature 006: karaoke reads the transport clock, never the other way round. Feature 012: the
practice controller drives transport, metronome and the stretch stage through narrow views, so
none of them (nor karaoke) imports ``practice/`` or ``stretch/``; ``practice/`` never imports
``karaoke/``.
"""


TS_MODULE_IMPORTERS: Mapping[str, frozenset[str]] = {
    "audio-engine/recording": frozenset({"audio-engine/engine"}),
    "audio-engine/engine": frozenset({"audio-engine/index"}),
}
"""Modules only the listed modules (and the module itself) may import (feature 015, T007:
the recording core is reached through ``engine.ts`` only, never from ``index.ts``/``callbacks``;
feature 016, T009: nothing but ``index.ts`` imports ``engine.ts``)."""


@dataclass(frozen=True, slots=True)
class WorkletScope:
    """Files of one folder that run in AudioWorklet scope (file-level allow-list)."""

    package: str
    folder: str
    entry: str
    modules: frozenset[str]


TS_WORKLET = WorkletScope(
    "audio-engine",
    "stretch",
    "processor",
    frozenset({"processor", "constants", "params", "fifo", "abi", "core"}),
)
"""Feature 012 worklet scope: ``core`` (the WSOLA driver of audio-engine) joined the plan list."""

_TYPE_ONLY = re.compile(r"\s*(?:import|export)\s+type\b")

_TS_SUFFIXES = (".tsx", ".ts", ".js")

TS_SCOPE = "@stemstudio/"
"""npm scope of the StemStudio TypeScript packages."""

_TS_COMMENT = re.compile(r"/\*.*?\*/|^[ \t]*//[^\n]*", re.DOTALL | re.MULTILINE)
_TS_IMPORTS = (
    re.compile(r"^[ \t]*(?:import|export)\b[^;'\"]*?\bfrom\s*(['\"])(?P<spec>[^'\"]+)\1", re.M),
    re.compile(r"^[ \t]*import\s*(['\"])(?P<spec>[^'\"]+)\1", re.M),
    re.compile(r"\bimport\s*\(\s*(['\"])(?P<spec>[^'\"]+)\1"),
)
"""Static ``import``/``export ... from``, side-effect ``import '...'`` and dynamic ``import()``."""


@dataclass(frozen=True, slots=True)
class Edge:
    """A dependency ``source -> target`` with its location.

    ``module`` is the imported dotted module below the worker package (``metadata.http``) for
    worker edges and empty otherwise.
    """

    source: str
    target: str
    path: str
    line: int
    module: str = ""


def read_dotnet_edges(src_dir: Path, root: Path) -> list[Edge]:
    """Return the ``ProjectReference`` edges of every ``*.csproj`` directly below ``src_dir``."""
    edges: list[Edge] = []
    for project in sorted(src_dir.glob("*/*.csproj")):
        tree = ET.parse(project)  # noqa: S314  (trusted repository files)
        for reference in tree.iter("ProjectReference"):
            target = PureWindowsPath(reference.get("Include", "")).stem
            edges.append(Edge(project.stem, target, relative(project, root), 0))
    return edges


def read_package_edges(src_dir: Path, root: Path) -> list[Edge]:
    """Return the ``PackageReference`` edges (project -> package) of the ``src/*/*.csproj``."""
    edges: list[Edge] = []
    for project in sorted(src_dir.glob("*/*.csproj")):
        tree = ET.parse(project)  # noqa: S314  (trusted repository files)
        path = relative(project, root)
        edges.extend(
            Edge(project.stem, ref.get("Include", ""), path, 0)
            for ref in tree.iter("PackageReference")
        )
    return edges


def check_packages(edges: Sequence[Edge], allowed: Mapping[str, frozenset[str]]) -> list[Finding]:
    """Return an error per package outside its project's allow-list (unlisted: unrestricted)."""
    return [
        Finding("dependency", Severity.ERROR, edge.path, edge.line, _package_message(edge))
        for edge in edges
        if edge.source in allowed and not _in_family(edge.target, allowed[edge.source])
    ]


def _package_message(edge: Edge) -> str:
    return f"forbidden package {edge.source} -> {edge.target}"


def _in_family(name: str, families: frozenset[str]) -> bool:
    return any(name == family or name.startswith(family + ".") for family in families)


def read_folder_edges(src_dir: Path, root: Path, folders: Sequence[str]) -> list[Edge]:
    """Return the StemStudio and third-party namespace references of the C# files per folder.

    ``folders`` are relative to ``src_dir`` (``StemStudio.Api/Metadata``); an edge is
    ``folder -> StemStudio.<Project>[.<...>]`` (the whole dotted name) or ``folder -> OllamaSharp``
    (the third-party root) at the referencing line. Excluded files (``obj/``, ``*.g.cs``) and
    files below the folder's ``DOTNET_FOLDER_EXEMPT`` sub-folders contribute no edges.
    """
    edges: list[Edge] = []
    for folder in folders:
        for cs_file, rel_path in _folder_files(src_dir, root, folder):
            edges.extend(_cs_file_edges(cs_file, folder, rel_path))
    return edges


_CS_NAME_PATTERNS = (_CS_EXTERNAL_QUALIFIED, _CS_EXTERNAL_USING, _CS_HTTP_TYPE, _CS_HTTP_NAMESPACE)
"""Patterns whose ``name`` group is an edge target besides the StemStudio namespaces."""

_FOLDER_SUFFIXES = ("*.cs", "*.razor")
"""File types a feature-folder rule reads (Razor components reference namespaces like C#)."""


def _folder_files(src_dir: Path, root: Path, folder: str) -> Iterator[tuple[Path, str]]:
    """The checked C# files below ``folder`` with their repository paths."""
    exempt = [src_dir / name for name in DOTNET_FOLDER_EXEMPT.get(folder, ())]
    files = sorted(f for suffix in _FOLDER_SUFFIXES for f in (src_dir / folder).rglob(suffix))
    for cs_file in files:
        rel_path = relative(cs_file, root)
        if not is_excluded(rel_path) and not _below_any(cs_file, exempt):
            yield cs_file, rel_path


def _below_any(path: Path, folders: Sequence[Path]) -> bool:
    return any(path.is_relative_to(folder) for folder in folders)


def _cs_file_edges(cs_file: Path, folder: str, rel_path: str) -> Iterator[Edge]:
    text = _CS_NOISE.sub(_keep_newlines, cs_file.read_text(encoding="utf-8"))
    references = [(m.start(), m.group()) for m in _CS_REFERENCE.finditer(text)]
    for pattern in _CS_NAME_PATTERNS:
        references.extend((m.start("name"), m.group("name")) for m in pattern.finditer(text))
    for start, target in sorted(references):
        yield Edge(folder, target, rel_path, text.count("\n", 0, start) + 1)


def check_forbidden(
    edges: Sequence[Edge], forbidden: Mapping[str, frozenset[str]]
) -> list[Finding]:
    """Return an error for every edge whose target is (below) a forbidden name of its source.

    A forbidden name ``a.b`` matches the target ``a.b`` and ``a.b.<anything>``; the message names
    the matched forbidden name.
    """
    hits = ((edge, _forbidden_hit(edge, forbidden)) for edge in edges)
    return [
        Finding(
            "dependency",
            Severity.ERROR,
            edge.path,
            edge.line,
            f"{_FORBIDDEN} {edge.source} -> {hit}",
        )
        for edge, hit in hits
        if hit is not None
    ]


_FORBIDDEN = "forbidden dependency"


def check_allowed(
    edges: Sequence[Edge], allow_lists: Mapping[str, tuple[str, frozenset[str]]]
) -> list[Finding]:
    """Return an error for every edge of an allow-listed folder that leaves its allow-list."""
    return [
        Finding(
            "dependency",
            Severity.ERROR,
            edge.path,
            edge.line,
            f"{_FORBIDDEN} {edge.source} -> {edge.target}",
        )
        for edge in edges
        if edge.source in allow_lists and not _allow_listed(edge, *allow_lists[edge.source])
    ]


def _allow_listed(edge: Edge, scope: str, allowed: frozenset[str]) -> bool:
    own = scope + "." + edge.source.split("/", 1)[1].replace("/", ".")
    inside = _in_family(edge.target, frozenset({scope}))
    return not inside or _in_family(edge.target, allowed | {own})


def _forbidden_hit(edge: Edge, forbidden: Mapping[str, frozenset[str]]) -> str | None:
    names = sorted(forbidden.get(edge.source, frozenset()))
    target = edge.target
    return next((n for n in names if target == n or target.startswith(n + ".")), None)


def read_python_edges(package_dir: Path, root: Path, package: str = PYTHON_PACKAGE) -> list[Edge]:
    """Return the internal import edges between top-level modules of the package ``package``.

    ``package_dir`` is the package's folder (worker by default). Excluded files (vendored
    third-party code) contribute no edges.
    """
    return [
        Edge(module.source, parts[0], module.path, line, ".".join(parts))
        for module in _python_modules(package_dir, root)
        for parts, line in _imports(module.tree, module.rel, package)
        if parts and parts[0] != module.source
    ]


def read_foreign_edges(package_dir: Path, root: Path, foreign: str) -> list[Edge]:
    """Return the absolute imports of the package ``foreign`` (top-level module -> ``foreign``)."""
    return [
        Edge(module.source, target, module.path, line)
        for module in _python_modules(package_dir, root)
        for target, line in _external_imports(module.tree)
        if target == foreign
    ]


def check_foreign(edges: Sequence[Edge]) -> list[Finding]:
    """Return an error per import of the other service's package."""
    findings = (
        Finding(
            "dependency",
            Severity.ERROR,
            edge.path,
            edge.line,
            f"forbidden dependency {edge.source} -> {edge.target}",
        )
        for edge in edges
    )
    return list(dict.fromkeys(findings))


def read_external_edges(package_dir: Path, root: Path) -> list[Edge]:
    """Return the absolute non-worker imports of the modules in ``PYTHON_EXTERNAL_ALLOWED``.

    The target is the imported top-level name (``numpy`` for ``import numpy.typing``);
    relative imports and ``stemstudio_worker`` imports are not external.
    """
    return [
        Edge(module.source, target, module.path, line)
        for module in _python_modules(package_dir, root)
        if module.source in PYTHON_EXTERNAL_ALLOWED
        for target, line in _external_imports(module.tree)
        if target != PYTHON_PACKAGE
    ]


def _external_imports(tree: ast.Module) -> Iterator[tuple[str, int]]:
    for node in ast.walk(tree):
        yield from _node_externals(node)


def _node_externals(node: ast.AST) -> list[tuple[str, int]]:
    """Top-level names of an absolute import (``from x.y import z`` -> ``x``)."""
    if isinstance(node, ast.Import):
        return [(alias.name.split(".")[0], node.lineno) for alias in node.names]
    if isinstance(node, ast.ImportFrom):
        return _from_external(node)
    return []


def _from_external(node: ast.ImportFrom) -> list[tuple[str, int]]:
    if node.level or node.module is None:
        return []
    return [(node.module.split(".")[0], node.lineno)]


def check_external(edges: Sequence[Edge]) -> list[Finding]:
    """Return an error per third-party import outside the module's allow-list."""
    findings = (
        Finding(
            "dependency",
            Severity.ERROR,
            edge.path,
            edge.line,
            f"forbidden third-party dependency {edge.source} -> {edge.target}",
        )
        for edge in edges
        if not _external_allowed(edge)
    )
    return list(dict.fromkeys(findings))


def _external_allowed(edge: Edge) -> bool:
    return edge.target in STDLIB or edge.target in PYTHON_EXTERNAL_ALLOWED[edge.source]


@dataclass(frozen=True, slots=True)
class _Module:
    """A parsed worker file: top-level module, repository path, package path and AST."""

    source: str
    path: str
    rel: Path
    tree: ast.Module


def _python_modules(package_dir: Path, root: Path) -> Iterator[_Module]:
    for module_file in sorted(package_dir.rglob("*.py")):
        rel_path = relative(module_file, root)
        if not is_excluded(rel_path):
            rel = module_file.relative_to(package_dir)
            tree = ast.parse(module_file.read_text(encoding="utf-8"))
            yield _Module(_top_module(rel), rel_path, rel, tree)


def read_ts_edges(src_dir: Path, root: Path) -> list[Edge]:
    """Return the cross-package import edges of the TypeScript packages directly below ``src_dir``.

    Excluded files (``node_modules/``, ``dist/``, ``*.d.ts``) contribute no edges.
    """
    edges: list[Edge] = []
    for package in sorted(manifest.parent for manifest in src_dir.glob("*/package.json")):
        for ts_file in _ts_files(package, root):
            edges.extend(_ts_file_edges(ts_file, package, root))
    return edges


def read_ts_module_edges(src_dir: Path, root: Path) -> list[Edge]:
    """Return the module edges inside every package named in ``TS_MODULE_FORBIDDEN``.

    Only files below ``src/<package>/src/`` count (tests are not modules); an edge is
    ``<package>/<module> -> <package>/<module>`` for a relative import between two different
    top-level modules.
    """
    edges: list[Edge] = []
    for package in sorted({key.split("/")[0] for key in TS_MODULE_FORBIDDEN}):
        code = src_dir / package / "src"
        for ts_file in _ts_files(code, root) if code.is_dir() else ():
            edges.extend(_ts_module_edges(ts_file, code, package, root))
    return edges


def _ts_module_edges(ts_file: Path, code: Path, package: str, root: Path) -> Iterator[Edge]:
    text = _TS_COMMENT.sub(_keep_newlines, ts_file.read_text(encoding="utf-8"))
    source = _ts_module(ts_file.resolve(), code.resolve())
    rel_path = relative(ts_file, root)
    for pattern in _TS_IMPORTS:
        for match in pattern.finditer(text):
            target = _ts_relative_module(match.group("spec"), ts_file, code)
            if target not in (None, source):
                line = text.count("\n", 0, match.start()) + 1
                yield Edge(f"{package}/{source}", f"{package}/{target}", rel_path, line)


def check_worklet(src_dir: Path, root: Path, scope: WorkletScope = TS_WORKLET) -> list[Finding]:
    """Return an error for every import of a worklet file that leaves the worklet scope."""
    folder = src_dir / scope.package / "src" / scope.folder
    files = ((folder / f"{name}.ts", name) for name in sorted(scope.modules))
    return [
        finding
        for path, name in files
        if path.is_file()
        for finding in _worklet_findings(path, name == scope.entry, scope, root)
    ]


def _worklet_findings(path: Path, strict: bool, scope: WorkletScope, root: Path) -> list[Finding]:
    text = _TS_COMMENT.sub(_keep_newlines, path.read_text(encoding="utf-8"))
    rel_path = relative(path, root)
    return [
        Finding("dependency", Severity.ERROR, rel_path, line, f"{_WORKLET} {path.name} {message}")
        for line, message in _worklet_breaches(text, path, strict, scope.modules)
    ]


_WORKLET = "worklet scope:"


def _worklet_breaches(
    text: str, path: Path, strict: bool, modules: frozenset[str]
) -> Iterator[tuple[int, str]]:
    """(line, ``must not import <label>``) per import that leaves ``modules``."""
    for match in _bundled_imports(text, strict):
        inside, label = _worklet_target(match.group("spec"), path)
        if not (inside and label in modules):
            yield text.count("\n", 0, match.start()) + 1, f"must not import {label}"


def _bundled_imports(text: str, strict: bool) -> Iterator[re.Match[str]]:
    """Every import (``strict``) or only those the build keeps (no ``import type``)."""
    return (m for m in _all_imports(text) if strict or not _TYPE_ONLY.match(m.group()))


def _all_imports(text: str) -> Iterator[re.Match[str]]:
    return (match for pattern in _TS_IMPORTS for match in pattern.finditer(text))


def _worklet_target(spec: str, path: Path) -> tuple[bool, str]:
    """(inside the worklet folder, module label) of an import specifier."""
    if not spec.startswith("."):
        return False, spec
    target = (path.parent / spec).resolve()
    folder, code = path.parent.resolve(), path.parent.parent.resolve()
    base = folder if target.is_relative_to(folder) else code
    label = target.relative_to(base).as_posix() if target.is_relative_to(base) else spec
    for suffix in _TS_SUFFIXES:
        label = label.removesuffix(suffix)
    return base == folder, label


def _ts_relative_module(spec: str, ts_file: Path, code: Path) -> str | None:
    """Top-level module a relative specifier points to (None: npm package or outside)."""
    if not spec.startswith("."):
        return None
    try:
        return _ts_module((ts_file.parent / spec).resolve(), code.resolve())
    except ValueError:
        return None


def _ts_module(path: Path, code: Path) -> str:
    """``src/transport.ts`` and ``src/transport/clock.ts`` -> ``transport``."""
    first = path.relative_to(code).parts[0]
    for suffix in _TS_SUFFIXES:
        first = first.removesuffix(suffix)
    return first


def _ts_files(package: Path, root: Path) -> Iterator[Path]:
    for folder, dirs, files in package.walk():
        dirs[:] = sorted(name for name in dirs if name not in EXCLUDED_DIRS)
        yield from _ts_in(folder, files, root)


def _ts_in(folder: Path, files: Sequence[str], root: Path) -> list[Path]:
    paths = (folder / name for name in sorted(files) if name.endswith((".ts", ".tsx")))
    return [path for path in paths if not is_excluded(relative(path, root))]


def _ts_file_edges(ts_file: Path, package: Path, root: Path) -> Iterator[Edge]:
    text = _TS_COMMENT.sub(_keep_newlines, ts_file.read_text(encoding="utf-8"))
    rel_path = relative(ts_file, root)
    for pattern in _TS_IMPORTS:
        for match in pattern.finditer(text):
            spec = match.group("spec")
            if _allowed_data_import(spec, ts_file, package):
                continue
            target = _ts_target(spec, ts_file, package.parent)
            if target is not None and target != package.name:
                line = text.count("\n", 0, match.start()) + 1
                yield Edge(package.name, target, rel_path, line)


def _keep_newlines(match: re.Match[str]) -> str:
    return "\n" * match.group().count("\n")


def _allowed_data_import(spec: str, ts_file: Path, package: Path) -> bool:
    """True for a relative import of a data file listed in ``TS_DATA_IMPORTS_ALLOWED``."""
    allowed = TS_DATA_IMPORTS_ALLOWED.get(package.name, frozenset())
    if not spec.startswith(".") or not allowed:
        return False
    try:
        rel = (ts_file.parent / spec).resolve().relative_to(package.parent.resolve())
    except ValueError:
        return False
    return rel.as_posix() in allowed


def _ts_target(spec: str, ts_file: Path, src_dir: Path) -> str | None:
    """Return the package a specifier points into (None: npm package or outside ``src``)."""
    if spec.startswith(TS_SCOPE):
        return spec.removeprefix(TS_SCOPE).split("/")[0]
    if not spec.startswith("."):
        return None
    try:
        return (ts_file.parent / spec).resolve().relative_to(src_dir.resolve()).parts[0]
    except ValueError:
        return None


def _top_module(rel: Path) -> str:
    """``jobs/runner.py`` -> ``jobs``, ``main.py`` -> ``main``, ``__init__.py`` -> ``__init__``."""
    return rel.parts[0].removesuffix(".py")


def _imports(tree: ast.Module, rel: Path, package: str) -> Iterator[tuple[list[str], int]]:
    """Yield the imported module parts below the package (``["metadata", "http"]``) per name."""
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            for alias in node.names:
                yield from _absolute_target(alias.name, node.lineno, package)
        elif isinstance(node, ast.ImportFrom):
            yield from _from_targets(node, rel, package)


def _absolute_target(dotted: str, line: int, package: str) -> Iterator[tuple[list[str], int]]:
    parts = dotted.split(".")
    if parts[0] == package:
        yield parts[1:], line


def _from_targets(node: ast.ImportFrom, rel: Path, package: str) -> Iterator[tuple[list[str], int]]:
    """Targets of ``from X import a, b``: ``[*X, a]`` and ``[*X, b]`` below the package."""
    dotted = _resolve_from(node, rel)
    if dotted[:1] == [package]:
        dotted = dotted[1:]
    elif node.level == 0:
        return
    yield from (([*dotted, alias.name], node.lineno) for alias in node.names)


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
    found = (_edge_finding(edge, allowed) for edge in edges)
    findings = list(dict.fromkeys(f for f in found if f is not None))
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
    extra = PYTHON_SUBMODULES_ALLOWED.get(edge.source, frozenset())
    if _allows_module(extra, edge.module):
        return None
    message = f"forbidden dependency {edge.source} -> {_target_text(edge, extra)}"
    return Finding("dependency", Severity.ERROR, edge.path, edge.line, message)


def _allows_module(extra: frozenset[str], module: str) -> bool:
    return any(module == name or module.startswith(name + ".") for name in extra)


def _target_text(edge: Edge, extra: frozenset[str]) -> str:
    """``metadata.images (allowed: metadata.http, ...)`` when submodules of it are allowed."""
    names = sorted(name for name in extra if name.split(".")[0] == edge.target)
    if not names:
        return edge.target
    return f"{edge.module} (allowed: {', '.join(names)})"


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
    """Check the .NET, TypeScript, worker and generator dependency graphs below ``root``."""
    findings = check_edges(read_dotnet_edges(root / "src", root), DOTNET_ALLOWED)
    findings.extend(check_packages(read_package_edges(root / "src", root), DOTNET_PACKAGES_ALLOWED))
    folders = list(DOTNET_FOLDER_FORBIDDEN) + list(DOTNET_FOLDER_ALLOWED)
    folder_edges = read_folder_edges(root / "src", root, folders)
    findings.extend(check_forbidden(folder_edges, DOTNET_FOLDER_FORBIDDEN))
    findings.extend(check_allowed(folder_edges, DOTNET_FOLDER_ALLOWED))
    findings.extend(check_typescript(root))
    for package in PYTHON_PACKAGES:
        findings.extend(check_package(root, package))
    worker_dir = root / WORKER.path
    if worker_dir.is_dir():
        findings.extend(check_external(read_external_edges(worker_dir, root)))
        findings.extend(check_scopes(worker_dir, root, PYTHON_SCOPES))
    return findings


def check_importers(
    edges: Sequence[Edge], importers: Mapping[str, frozenset[str]]
) -> list[Finding]:
    """Return an error for every edge into a guarded module from a module not allowed to."""
    return [
        Finding("dependency", Severity.ERROR, edge.path, edge.line,
                f"{_FORBIDDEN} {edge.source} -> {edge.target} (imported only by "
                f"{', '.join(sorted(importers[edge.target]))})")
        for edge in edges
        if edge.target in importers and edge.source not in importers[edge.target]
    ]  # fmt: skip


def check_scopes(package_dir: Path, root: Path, scopes: Mapping[str, PythonScope]) -> list[Finding]:
    """Return an error per import of a scoped worker file outside its scope's allow-lists."""
    findings: list[Finding] = []
    for module in _python_modules(package_dir, root):
        key = _scope_of(module.rel.as_posix(), scopes)
        if key is not None:
            findings.extend(_scope_findings(module, key, scopes[key]))
    return list(dict.fromkeys(findings))


def _scope_of(rel: str, scopes: Mapping[str, PythonScope]) -> str | None:
    folder = (key for key in scopes if key.endswith("/") and rel.startswith(key))
    return rel if rel in scopes else next(folder, None)


def _scope_findings(module: "_Module", key: str, scope: PythonScope) -> Iterator[Finding]:
    yield from _scope_internal(module, key, scope.internal)
    yield from _scope_external(module, key, scope.external)


def _scope_internal(module: "_Module", key: str, allowed: frozenset[str]) -> Iterator[Finding]:
    for parts, line in _imports(module.tree, module.rel, PYTHON_PACKAGE):
        if parts and not _in_family(".".join(parts), allowed):
            yield _scope_finding(module, line, f"{_FORBIDDEN} {key} -> {'.'.join(parts)}")


def _scope_external(module: "_Module", key: str, allowed: frozenset[str]) -> Iterator[Finding]:
    for target, line in _external_imports(module.tree):
        if target not in {PYTHON_PACKAGE, *STDLIB, *allowed}:
            message = f"forbidden third-party dependency {key} -> {target}"
            yield _scope_finding(module, line, message)


def _scope_finding(module: "_Module", line: int, message: str) -> Finding:
    return Finding("dependency", Severity.ERROR, module.path, line, message)


def check_typescript(root: Path) -> list[Finding]:
    """Check the TypeScript packages: package edges, module edges and the worklet scope."""
    src_dir = root / "src"
    findings = check_edges(read_ts_edges(src_dir, root), TS_ALLOWED)
    module_edges = read_ts_module_edges(src_dir, root)
    findings.extend(check_forbidden(module_edges, TS_MODULE_FORBIDDEN))
    findings.extend(check_importers(module_edges, TS_MODULE_IMPORTERS))
    findings.extend(check_worklet(src_dir, root))
    return findings


def check_package(root: Path, package: PythonPackage) -> list[Finding]:
    """Check one Python service package: its layer map and imports of the other service."""
    package_dir = root / package.path
    if not package_dir.is_dir():
        return []
    edges = read_python_edges(package_dir, root, package.name)
    findings = check_edges(edges, package.allowed)
    findings.extend(check_foreign(read_foreign_edges(package_dir, root, package.foreign)))
    return findings


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    args = build_parser(__doc__, with_paths=False).parse_args(argv)
    return report_findings("dependency_check", run(args.root))


if __name__ == "__main__":
    sys.exit(main())
