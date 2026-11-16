"""011 follow-up claude-key: secret-handling rules (claude-key T017) [sec].

(a) Data Protection is confined to ``src/StemStudio.Api/Agent/Secrets/``: no other production file
    (C# or Razor) uses the ``Microsoft.AspNetCore.DataProtection`` namespace or its well-known
    entry points (``AddDataProtection`` lives in ``Microsoft.Extensions.DependencyInjection`` and
    needs no ``using``); a ``global using`` of it is an error even inside the folder, because it
    would leak to the whole project; only ``StemStudio.Api`` references the
    ``StemStudio.Api.Agent.Secrets`` namespace; no project but the API adds a Data Protection
    package.
(b) ``StoreClaudeKeyRequest`` and ``ClaudeApiKey`` are not ``record`` types (a record's generated
    ``ToString``/``PrintMembers`` would print the value).
(c) No logging call in ``Api/Agent/Secrets`` and ``Agent/Backends/Claude*`` passes an argument
    named ``key``/``apiKey`` (the last segment of a name, any case), calls ``Reveal()`` in an
    argument, or names ``{Key}``/``{ApiKey}`` in its template; ``[LoggerMessage]`` methods have no
    such parameter and are checked at their call sites too.

Ownership: the secrets folder is backend's, ``ClaudeApiKey`` and ``ClaudeKeyProvider`` are
ai-agent's, ``tests/Shared/ClaudeKey`` holds the architect's shared test doubles, ``scripts/`` is
devops' tested Python (measured since this follow-up).
"""

from pathlib import Path

import pytest
from repo_tree import write_file

import secret_rules
from common import DEFAULT_ROOTS, REPO_ROOT, Severity, owner_of
from components import COMPONENTS

SECRETS = "src/StemStudio.Api/Agent/Secrets"
BACKENDS = "src/StemStudio.Agent/Backends"


def _findings(root: Path, rel: str, text: str) -> list[tuple[str, int, str]]:
    write_file(root, rel, text)
    return [(f.message, f.line, f.owner) for f in secret_rules.run(root)]


def _only(root: Path, rel: str, text: str) -> tuple[str, int, str]:
    (finding,) = _findings(root, rel, text)
    return finding


# ------------------------------------------------------------------ (a) Data Protection confinement

DP_SETUP = """using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace StemStudio.Api.Agent.Secrets;

static class DataProtectionSetup
{
    static void Add(IServiceCollection services) =>
        services.AddDataProtection().SetApplicationName("StemStudio.Api");
}
"""


def test_data_protection_inside_the_secrets_folder_is_clean(tmp_path: Path) -> None:
    assert _findings(tmp_path, f"{SECRETS}/DataProtectionSetup.cs", DP_SETUP) == []
    assert _findings(tmp_path, f"{SECRETS}/Ring/KeyRingDirectory.cs", DP_SETUP) == []


@pytest.mark.parametrize(
    ("rel", "text", "line", "owner"),
    [
        (
            "src/StemStudio.Api/Agent/AgentRegistration.cs",
            "namespace X;\nusing Microsoft.AspNetCore.DataProtection;\n",
            2,
            "backend",
        ),
        (
            "src/StemStudio.Web/Program.cs",
            "// setup\nusing DP = Microsoft.AspNetCore.DataProtection.IDataProtector;\n",
            2,
            "frontend",
        ),
        (
            "src/StemStudio.Agent/Backends/ClaudeKeyProvider.cs",
            "class P\n{\n    Microsoft.AspNetCore.DataProtection.IDataProtector? _p;\n}\n",
            3,
            "ai-agent",
        ),
        (
            "src/StemStudio.Api/Program.cs",
            "\n\nbuilder.Services.AddDataProtection();\n",
            3,
            "backend",
        ),
        (
            "src/StemStudio.Web/Components/Settings/ClaudeKeySection.razor",
            "<p>Key</p>\n@inject IDataProtectionProvider Protection\n",
            2,
            "frontend",
        ),
        (
            "src/StemStudio.Persistence/Agent/AgentSecretStore.cs",
            "class S { IPersistedDataProtector? P; }\n",
            1,
            "backend",
        ),
    ],
)
def test_data_protection_outside_the_secrets_folder_fails(
    tmp_path: Path, rel: str, text: str, line: int, owner: str
) -> None:
    message, found_line, found_owner = _only(tmp_path, rel, text)
    assert message.startswith("Data Protection outside src/StemStudio.Api/Agent/Secrets/")
    assert (found_line, found_owner) == (line, owner)


def test_each_data_protection_reference_is_one_finding(tmp_path: Path) -> None:
    text = "using Microsoft.AspNetCore.DataProtection;\nclass A { IDataProtector? P; }\n"
    lines = [line for _, line, _ in _findings(tmp_path, "src/StemStudio.Web/A.cs", text)]
    assert lines == [1, 2]


def test_global_using_of_data_protection_fails_even_in_the_secrets_folder(tmp_path: Path) -> None:
    text = "global using Microsoft.AspNetCore.DataProtection;\n"
    message, line, owner = _only(tmp_path, f"{SECRETS}/GlobalUsings.cs", text)
    assert message == (
        "global using of Microsoft.AspNetCore.DataProtection leaks Data Protection "
        "to the whole project"
    )
    assert (line, owner) == (1, "backend")


DP_MENTIONS = '''// Data Protection (Microsoft.AspNetCore.DataProtection) is set up in Agent/Secrets only.
/* AddDataProtection() and IDataProtector live there. */
class Notes
{
    const string Text = "Microsoft.AspNetCore.DataProtection";
    const string More = @"AddDataProtection";
    const string Raw = """IDataProtectionProvider""";
    int DataProtectionCount;
    string ProtectKeys = nameof(DataProtectionNotes);
}
'''


def test_comments_strings_and_lookalike_names_are_not_references(tmp_path: Path) -> None:
    assert _findings(tmp_path, "src/StemStudio.Api/Agent/Notes.cs", DP_MENTIONS) == []


def test_tests_may_use_data_protection(tmp_path: Path) -> None:
    rel = "tests/StemStudio.Api.Tests/Agent/Secrets/DataProtectionSetupTests.cs"
    assert _findings(tmp_path, rel, DP_SETUP) == []


def test_generated_and_build_output_files_are_skipped(tmp_path: Path) -> None:
    assert _findings(tmp_path, "src/StemStudio.Web/obj/Debug/X.cs", DP_SETUP) == []
    assert _findings(tmp_path, "src/StemStudio.Web/Pages/Index.g.cs", DP_SETUP) == []


@pytest.mark.parametrize(
    ("rel", "owner"),
    [
        ("src/StemStudio.Web/Pages/Settings.razor.cs", "frontend"),
        ("src/StemStudio.Agent/X.cs", "ai-agent"),
    ],
)
def test_secrets_namespace_outside_the_api_fails(tmp_path: Path, rel: str, owner: str) -> None:
    text = "namespace StemStudio.Web;\n\nusing StemStudio.Api.Agent.Secrets;\n"
    message, line, found_owner = _only(tmp_path, rel, text)
    assert message == "forbidden dependency on StemStudio.Api.Agent.Secrets outside StemStudio.Api"
    assert (line, found_owner) == (3, owner)


def test_secrets_namespace_inside_the_api_is_clean(tmp_path: Path) -> None:
    text = "using StemStudio.Api.Agent.Secrets;\nclass C { StemStudio.Api.Agent.Secrets.X? X; }\n"
    assert _findings(tmp_path, "src/StemStudio.Api/ApiComposition.cs", text) == []


def test_secrets_namespace_lookalike_is_not_a_reference(tmp_path: Path) -> None:
    text = "using StemStudio.Api.Agent.SecretsView;\n"
    assert _findings(tmp_path, "src/StemStudio.Web/A.cs", text) == []


CSPROJ = '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup>{items}</ItemGroup></Project>\n'


@pytest.mark.parametrize(
    "package",
    ["Microsoft.AspNetCore.DataProtection", "Microsoft.AspNetCore.DataProtection.Extensions"],
)
def test_data_protection_package_outside_the_api_fails(tmp_path: Path, package: str) -> None:
    items = f'<PackageReference Include="{package}" />'
    rel = "src/StemStudio.Web/StemStudio.Web.csproj"
    message, line, owner = _only(tmp_path, rel, CSPROJ.format(items=items))
    assert message == f"forbidden package StemStudio.Web -> {package} (Data Protection: Api only)"
    assert (line, owner) == (0, "frontend")


def test_other_packages_and_the_api_project_are_clean(tmp_path: Path) -> None:
    web = '<PackageReference Include="Radzen.Blazor" />'
    write_file(tmp_path, "src/StemStudio.Web/StemStudio.Web.csproj", CSPROJ.format(items=web))
    api = '<PackageReference Include="Microsoft.AspNetCore.DataProtection" />'
    write_file(tmp_path, "src/StemStudio.Api/StemStudio.Api.csproj", CSPROJ.format(items=api))
    assert secret_rules.run(tmp_path) == []


# ------------------------------------------------------------------ (b) no record value carriers


@pytest.mark.parametrize(
    "declaration",
    [
        "public sealed record ClaudeApiKey",
        "public record class ClaudeApiKey",
        "public readonly record struct ClaudeApiKey",
        "internal sealed partial record ClaudeApiKey",
        "public record ClaudeApiKey(string Value);",
    ],
)
def test_claude_api_key_as_record_fails(tmp_path: Path, declaration: str) -> None:
    text = f"namespace StemStudio.Agent.Model;\n\n{declaration}\n{{\n}}\n"
    message, line, owner = _only(tmp_path, "src/StemStudio.Agent/Model/ClaudeApiKey.cs", text)
    assert (
        message
        == "ClaudeApiKey must not be a record (a record's generated ToString prints the value)"
    )
    assert (line, owner) == (3, "ai-agent")


def test_store_request_as_record_fails(tmp_path: Path) -> None:
    text = "public sealed record StoreClaudeKeyRequest(string ApiKey);\n"
    rel = "src/StemStudio.Contracts/Agent/StoreClaudeKeyRequest.cs"
    message, line, owner = _only(tmp_path, rel, text)
    assert message.startswith("StoreClaudeKeyRequest must not be a record")
    assert (line, owner) == (1, "architect")


CLASSES = """// Never a record ClaudeApiKey: see research.
public sealed class ClaudeApiKey { }
public sealed class StoreClaudeKeyRequest { }
public sealed record ClaudeApiKeyView(string LastFour);
public sealed record ClaudeKeyResolution { public ClaudeApiKey? Key { get; init; } }
"""


def test_classes_and_other_records_are_clean(tmp_path: Path) -> None:
    assert _findings(tmp_path, "src/StemStudio.Agent/Model/Types.cs", CLASSES) == []


def test_record_rule_applies_to_production_code_only(tmp_path: Path) -> None:
    text = "sealed record ClaudeApiKey(string V);\n"
    assert _findings(tmp_path, "tests/Shared/ClaudeKey/Spy.cs", text) == []


# ------------------------------------------------------------------ (c) no key in log calls


@pytest.mark.parametrize(
    "call",
    [
        '_logger.LogInformation("Saved", key);',
        'logger.LogWarning("Saved {Length}", apiKey.Length > 0, ApiKey);',
        'logger.Log(LogLevel.Debug, "Saved {V}", request.ApiKey);',
        'logger.LogDebug("Saved {V}", resolution.Key);',
        'logger.LogDebug("Saved {V}", value.Reveal());',
        'logger.LogDebug("Saved {V}", Format(key.Reveal()));',
        'logger.LogError(ex, "Saved {Key}", 1);',
        'logger.LogError("Saved {@ApiKey}", 1);',
        'logger.LogTrace("Saved {apikey:l}", 1);',
        'using var scope = logger.BeginScope("{V}", _apiKey);',
        "LogStored(_logger, apiKey);",
    ],
)
def test_key_in_a_log_call_fails(tmp_path: Path, call: str) -> None:
    text = f"""partial class Store
{{
    void Save()
    {{
        {call}
    }}

    [LoggerMessage(EventId = 1, Message = "Stored")]
    private static partial void LogStored(ILogger logger, bool replaced);
}}
"""
    message, line, owner = _only(tmp_path, f"{SECRETS}/ProtectedClaudeKeyStore.cs", text)
    assert message == "log call passes the Claude key or names it in its template"
    assert (line, owner) == (5, "backend")


def test_multi_line_call_reports_its_first_line(tmp_path: Path) -> None:
    text = (
        'class C\n{\n    void M() => _logger.LogWarning(\n        "x {V}",\n        apiKey);\n}\n'
    )
    assert _only(tmp_path, f"{BACKENDS}/ClaudeChatClientFactory.cs", text)[1] == 3


@pytest.mark.parametrize(
    ("signature", "template"),
    [
        ("ILogger logger, string apiKey", "Stored"),
        ("ILogger logger, ClaudeApiKey key", "Stored"),
        ("ILogger logger, bool replaced", "Stored {Key}"),
    ],
)
def test_logger_message_with_a_key_fails(tmp_path: Path, signature: str, template: str) -> None:
    text = f"""partial class Store
{{
    [LoggerMessage(EventId = 4100, Level = LogLevel.Information,
        Message = "{template}")]
    private static partial void LogStored({signature});
}}
"""
    message, line, owner = _only(tmp_path, f"{SECRETS}/ProtectedClaudeKeyStore.cs", text)
    assert message == "[LoggerMessage] method LogStored takes or names the Claude key"
    assert (line, owner) == (3, "backend")


CLEAN_LOGGING = """partial class Store
{
    void Save(ClaudeApiKey key, bool replaced)
    {
        // logger.LogInformation("{Key}", key);
        _logger.LogInformation("Saved (last four: {LastFour})", key.LastFour);
        _logger.LogWarning("Unreadable ({FailureType})", ex.GetType().Name);
        logger.LogInformation("Key ring at {Directory}", keyRing.Path);
        logger.LogInformation("{KeySource} {Keys}", source, keys.Count);
        LogStored(_logger, replaced);
        var catalog = Logs(key);
        Store(key);
        Login(apiKey);
    }

    [LoggerMessage(EventId = 4100, Message = "Claude key stored (replaced: {Replaced})")]
    private static partial void LogStored(ILogger logger, bool replaced);
}
"""


def test_clean_logging_and_non_log_calls_pass(tmp_path: Path) -> None:
    assert _findings(tmp_path, f"{SECRETS}/ProtectedClaudeKeyStore.cs", CLEAN_LOGGING) == []


BAD_LOG = 'class C { void M() => logger.LogInformation("{Key}", key); }\n'


@pytest.mark.parametrize(
    "rel",
    [
        f"{BACKENDS}/ClaudeBackendProbe.cs",
        f"{BACKENDS}/Claude/ClaudeSession.cs",
        f"{SECRETS}/Filters/LocalOnlyKeyEntryFilter.cs",
    ],
)
def test_log_rule_scope_includes(tmp_path: Path, rel: str) -> None:
    assert len(_findings(tmp_path, rel, BAD_LOG)) == 1


@pytest.mark.parametrize(
    "rel",
    [
        f"{BACKENDS}/OllamaChatClientFactory.cs",
        "src/StemStudio.Api/Agent/AgentRunExecutor.cs",
        "src/StemStudio.Agent/Runtime/ClaudeRun.cs",
        "tests/StemStudio.Api.Tests/Agent/Secrets/ProtectedClaudeKeyStoreTests.cs",
    ],
)
def test_log_rule_scope_excludes(tmp_path: Path, rel: str) -> None:
    assert _findings(tmp_path, rel, BAD_LOG) == []


def test_unbalanced_call_does_not_crash(tmp_path: Path) -> None:
    text = 'class C { void M() => logger.LogInformation("x", key\n'
    assert _only(tmp_path, f"{SECRETS}/Broken.cs", text)[1] == 1


# ------------------------------------------------------------------ gate wiring, real tree


def test_findings_are_errors_of_check_secret(tmp_path: Path) -> None:
    write_file(tmp_path, f"{SECRETS}/A.cs", BAD_LOG)
    (finding,) = secret_rules.run(tmp_path)
    assert (finding.check, finding.severity, finding.path) == (
        "secret",
        Severity.ERROR,
        f"{SECRETS}/A.cs",
    )


def test_main_exit_codes(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert secret_rules.main(["--root", str(tmp_path)]) == 0
    write_file(tmp_path, f"{SECRETS}/A.cs", BAD_LOG)
    assert secret_rules.main(["--root", str(tmp_path)]) == 1
    assert "secret_rules: 1 errors, 0 warnings" in capsys.readouterr().out


def test_real_sources_are_clean() -> None:
    assert secret_rules.run(REPO_ROOT) == []


# ------------------------------------------------------------------ ownership and measured rows


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        (f"{SECRETS}/ProtectedClaudeKeyStore.cs", "backend"),
        ("src/StemStudio.Agent/Model/ClaudeApiKey.cs", "ai-agent"),
        ("src/StemStudio.Agent/Model/ClaudeKeyResolution.cs", "architect"),
        ("src/StemStudio.Agent/Backends/ClaudeKeyProvider.cs", "ai-agent"),
        ("src/StemStudio.Agent/Ports/IClaudeKeyProvider.cs", "architect"),
        ("tests/Shared/ClaudeKey/FakeClaudeKeyProvider.cs", "architect"),
        ("scripts/key_volume_checks.py", "devops"),
        ("scripts/tests/test_key_volume_checks.py", "devops"),
        ("scripts/secret_scan.py", "devops"),
    ],
)
def test_claude_key_owners(path: str, owner: str) -> None:
    assert owner_of(path) == owner


def test_scripts_are_measured_as_their_own_component() -> None:
    assert "scripts" in DEFAULT_ROOTS
    (row,) = [c for c in COMPONENTS if c.name == "scripts"]
    assert row.contains("scripts/key_volume_checks.py", test=False)
    assert row.contains("scripts/tests/test_key_volume_checks.py", test=True)


def test_secrets_folder_has_its_own_component_row() -> None:
    (row,) = [c for c in COMPONENTS if c.name == "Api/Agent/Secrets"]
    assert row.contains(f"{SECRETS}/DataProtectionSetup.cs", test=False)
    assert not row.contains("src/StemStudio.Api/Agent/AgentEndpoints.cs", test=False)
