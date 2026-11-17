"""Secret-handling rules of the Claude key (011 follow-up claude-key, plan.md, T017/T018).

Production files below ``src/`` (C# and Razor; comments and string literals are ignored)::

    (a) Data Protection only in src/StemStudio.Api/Agent/Secrets/: the namespace
        Microsoft.AspNetCore.DataProtection and its entry points (AddDataProtection,
        IDataProtector, ...) appear nowhere else; a ``global using`` of it is an error everywhere
        (it would leak to the whole project); StemStudio.Api.Agent.Secrets is referenced only
        from StemStudio.Api; no project but StemStudio.Api adds a Data Protection package.
    (b) StoreClaudeKeyRequest and ClaudeApiKey are not ``record`` types (the generated
        ToString/PrintMembers would print the value).
    (c) In Api/Agent/Secrets/** and Agent/Backends/Claude*: no log call (ILogger Log*, BeginScope,
        the file's [LoggerMessage] methods) passes an argument named key/apiKey (last name
        segment, any case, leading ``_``), calls Reveal() in an argument or names {Key}/{ApiKey}
        in its template; no [LoggerMessage] method takes such a parameter or names it.

Usage::

    python tools/quality/secret_rules.py [--root DIR]

Exit code 0 = clean, 1 = a rule is broken.
"""

import re
import sys
from collections.abc import Iterator, Sequence
from dataclasses import dataclass
from itertools import accumulate
from pathlib import Path

from common import Finding, Severity, build_parser, is_excluded, is_test, relative, report_findings
from dependency_check import read_package_edges

CHECK = "secret"
"""Check name of every finding of this module."""

SECRETS_DIR = "src/StemStudio.Api/Agent/Secrets/"
"""The only folder that may use Data Protection."""

API_DIR = "src/StemStudio.Api/"
SECRETS_NAMESPACE = "StemStudio.Api.Agent.Secrets"
DATA_PROTECTION = "Microsoft.AspNetCore.DataProtection"

DATA_PROTECTION_NAMES = (
    "AddDataProtection",
    "IDataProtectionBuilder",
    "IDataProtectionProvider",
    "IDataProtector",
    "IPersistedDataProtector",
    "DataProtectionProvider",
    "EphemeralDataProtectionProvider",
    "PersistKeysToFileSystem",
    "ProtectKeysWithDpapi",
)
"""Data Protection entry points usable without a ``using`` (extension methods, global usings)."""

VALUE_TYPES = ("ClaudeApiKey", "StoreClaudeKeyRequest")
"""Types that carry the key value and therefore must not be records."""

KEY_NAMES = frozenset({"key", "apikey"})
"""Forbidden argument/parameter names (lower case, without a leading ``_`` or ``@``)."""

LOG_DIRS = (SECRETS_DIR,)
CLAUDE_BACKENDS = "src/StemStudio.Agent/Backends/"

_LEXEMES = re.compile(
    "|".join(
        (
            r'(?P<s1>"""[\s\S]*?""")',
            r'(?P<s2>(?:\$@|@\$?)"(?:""|[^"])*")',
            r'(?P<s3>\$?"(?:\\.|[^"\\\n])*")',
            r"(?P<s4>'(?:\\.|[^'\\\n])+')",
            r"(?P<c1>//[^\n]*)",
            r"(?P<c2>/\*[\s\S]*?\*/)",
            r"(?P<c3>@\*[\s\S]*?\*@)",
        )
    )
)
"""C# string/char literals (``s*``) and C#/Razor comments (``c*``)."""

_DP_REFERENCE = re.compile(
    rf"\b{re.escape(DATA_PROTECTION)}\b|\b(?:{'|'.join(DATA_PROTECTION_NAMES)})\b"
)
_DP_GLOBAL_USING = re.compile(
    rf"^[ \t]*global[ \t]+using[ \t]+(?:static[ \t]+)?(?:\w+[ \t]*=[ \t]*)?(?:global::)?"
    rf"{re.escape(DATA_PROTECTION)}\b",
    re.MULTILINE,
)
_SECRETS_REFERENCE = re.compile(rf"\b{re.escape(SECRETS_NAMESPACE)}\b")
_RECORD = re.compile(rf"\brecord\s+(?:(?:class|struct)\s+)?(?P<name>{'|'.join(VALUE_TYPES)})\b")
_LOG_CALL = r"(?<!\w)(?:Log(?:Trace|Debug|Information|Warning|Error|Critical)?|BeginScope)"
_LOGGER_MESSAGE = re.compile(r"\[\s*LoggerMessage\b")
_VOID_METHOD = re.compile(r"\bvoid\s+(?P<name>\w+)\s*\(")
_PLACEHOLDER = re.compile(r"\{@?(?:api)?key(?:[,:][^}]*)?\}", re.IGNORECASE)
_NAME_PATH = re.compile(r"^@?[A-Za-z_]\w*(?:\??\.[A-Za-z_]\w*)*!?$")
_REVEAL = re.compile(r"\.\s*Reveal\s*\(")
_OPEN = "([{"
_CLOSE = ")]}"


@dataclass(frozen=True, slots=True)
class Source:
    """A source file in two views of equal length.

    ``code`` has comments and literals blanked, ``text`` only comments (template strings stay).
    """

    path: str
    code: str
    text: str

    def line(self, offset: int) -> int:
        """1-based line of ``offset``."""
        return self.code.count("\n", 0, offset) + 1

    def finding(self, offset: int, message: str) -> Finding:
        """An error of this check at ``offset``."""
        return Finding(CHECK, Severity.ERROR, self.path, self.line(offset), message)


def load(path: Path, root: Path) -> Source:
    """Read a C#/Razor file and blank its comments (both views) and literals (``code`` view)."""
    raw = path.read_text(encoding="utf-8")
    code: list[str] = []
    text: list[str] = []
    last = 0
    for match in _LEXEMES.finditer(raw):
        blank = _blank(match.group())
        literal = (match.lastgroup or "").startswith("s")
        code.extend((raw[last : match.start()], blank))
        text.extend((raw[last : match.start()], match.group() if literal else blank))
        last = match.end()
    code.append(raw[last:])
    text.append(raw[last:])
    return Source(relative(path, root), "".join(code), "".join(text))


def _blank(fragment: str) -> str:
    return re.sub(r"[^\n]", " ", fragment)


def run(root: Path) -> list[Finding]:
    """Check every production C#/Razor file and project file below ``root / "src"``."""
    findings: list[Finding] = []
    for source in production_sources(root):
        findings.extend(check_data_protection(source))
        findings.extend(check_records(source))
        if in_log_scope(source.path):
            findings.extend(check_logging(source))
    findings.extend(check_packages(root))
    return findings


def production_sources(root: Path) -> Iterator[Source]:
    """Every production C#/Razor file below ``root / "src"`` (also used by library_care_rules)."""
    src = root / "src"
    files = sorted(src.rglob("*.cs")) + sorted(src.rglob("*.razor")) if src.is_dir() else []
    for path in files:
        rel = relative(path, root)
        if not is_excluded(rel) and not is_test(rel):
            yield load(path, root)


# ------------------------------------------------------------------------- (a) Data Protection


def check_data_protection(source: Source) -> list[Finding]:
    """Data Protection outside the secrets folder, global usings, foreign secrets references.

    One finding per line; a global using wins over the other messages of its line.
    """
    found: dict[int, Finding] = {}
    for pattern, message in _dp_rules(source.path):
        for match in pattern.finditer(source.code):
            text = message.format(name=match.group())
            found.setdefault(source.line(match.start()), source.finding(match.start(), text))
    return [found[line] for line in sorted(found)]


def _dp_rules(path: str) -> list[tuple[re.Pattern[str], str]]:
    rules = [(_DP_GLOBAL_USING, _GLOBAL_USING_MESSAGE)]
    if not path.startswith(SECRETS_DIR):
        rules.append((_DP_REFERENCE, f"Data Protection outside {SECRETS_DIR} ({{name}})"))
    if not path.startswith(API_DIR):
        rules.append((_SECRETS_REFERENCE, _SECRETS_MESSAGE))
    return rules


_GLOBAL_USING_MESSAGE = (
    f"global using of {DATA_PROTECTION} leaks Data Protection to the whole project"
)
_SECRETS_MESSAGE = f"forbidden dependency on {SECRETS_NAMESPACE} outside StemStudio.Api"


def check_packages(root: Path) -> list[Finding]:
    """A Data Protection ``PackageReference`` in any ``src`` project but ``StemStudio.Api``."""
    src = root / "src"
    edges = read_package_edges(src, root) if src.is_dir() else []
    return [
        Finding(
            CHECK,
            Severity.ERROR,
            edge.path,
            0,
            f"forbidden package {edge.source} -> {edge.target} (Data Protection: Api only)",
        )
        for edge in edges
        if edge.source != "StemStudio.Api" and _is_data_protection(edge.target)
    ]


def _is_data_protection(package: str) -> bool:
    return package == DATA_PROTECTION or package.startswith(DATA_PROTECTION + ".")


# ------------------------------------------------------------------------- (b) records


def check_records(source: Source) -> list[Finding]:
    """A ``record`` declaration of a key value type."""
    return [
        source.finding(
            match.start(),
            f"{match.group('name')} must not be a record "
            "(a record's generated ToString prints the value)",
        )
        for match in _RECORD.finditer(source.code)
    ]


# ------------------------------------------------------------------------- (c) logging


def in_log_scope(path: str) -> bool:
    """Tell whether the logging rule applies (secrets folder, ``Agent/Backends/Claude*``)."""
    if path.startswith(LOG_DIRS):
        return True
    if not path.startswith(CLAUDE_BACKENDS):
        return False
    return any(part.startswith("Claude") for part in path[len(CLAUDE_BACKENDS) :].split("/"))


def check_logging(source: Source) -> list[Finding]:
    """Log calls and ``[LoggerMessage]`` methods that pass or name the key."""
    methods = list(_logger_messages(source))
    names = [method.name for method in methods]
    findings = [method.finding(source) for method in methods if method.leaks]
    findings.extend(_leaking_calls(source, names))
    return sorted(findings, key=lambda finding: finding.line)


@dataclass(frozen=True, slots=True)
class _LoggerMessage:
    """A ``[LoggerMessage]`` method: name, attribute offset and whether it takes/names the key."""

    name: str
    offset: int
    leaks: bool

    def finding(self, source: Source) -> Finding:
        message = f"[LoggerMessage] method {self.name} takes or names the Claude key"
        return source.finding(self.offset, message)


def _logger_messages(source: Source) -> Iterator[_LoggerMessage]:
    for attribute in _LOGGER_MESSAGE.finditer(source.code):
        close = _closing(source.code, attribute.start(), "[", "]")
        method = _VOID_METHOD.search(source.code, close)
        if method is None:
            continue
        params = _arguments(source.code, method.end() - 1)
        template = source.text[attribute.start() : close]
        leaks = bool(_PLACEHOLDER.search(template)) or any(_param_is_key(p) for p in params)
        yield _LoggerMessage(method.group("name"), attribute.start(), leaks)


def _param_is_key(parameter: str) -> bool:
    words = re.findall(r"[A-Za-z_]\w*", parameter.split("=", maxsplit=1)[0])
    return bool(words) and _is_key_name(words[-1])


def _leaking_calls(source: Source, methods: Sequence[str]) -> Iterator[Finding]:
    names = "|".join([_LOG_CALL, *(re.escape(name) for name in methods)])
    for call in re.finditer(rf"(?<![\w.])(?:[\w.?]+\.)?(?:{names})\s*\(", source.code):
        if not _is_declaration(source.code, call.start()) and _call_leaks(source, call.end() - 1):
            message = "log call passes the Claude key or names it in its template"
            yield source.finding(call.start(), message)


def _call_leaks(source: Source, open_paren: int) -> bool:
    close = _closing(source.code, open_paren, "(", ")")
    if _PLACEHOLDER.search(source.text[open_paren:close]):
        return True
    return any(_argument_is_key(argument) for argument in _arguments(source.code, open_paren))


def _is_declaration(code: str, offset: int) -> bool:
    return re.search(r"\bvoid\s*$", code[max(0, offset - 16) : offset]) is not None


def _argument_is_key(argument: str) -> bool:
    if _REVEAL.search(argument):
        return True
    expression = argument.strip()
    if not _NAME_PATH.match(expression):
        return False
    return _is_key_name(re.split(r"\??\.", expression.rstrip("!"))[-1])


def _is_key_name(name: str) -> bool:
    return name.lstrip("@_").lower() in KEY_NAMES


def _closing(code: str, start: int, opener: str, closer: str) -> int:
    """Offset just after the bracket matching the first ``opener`` at/after ``start``."""
    depth = 0
    for index in range(code.index(opener, start), len(code)):
        depth += (code[index] == opener) - (code[index] == closer)
        if depth == 0:
            return index + 1
    return len(code)


def _arguments(code: str, open_paren: int) -> list[str]:
    """Top-level comma-separated arguments of the parenthesis at ``open_paren``."""
    body = code[open_paren + 1 : _closing(code, open_paren, "(", ")") - 1]
    cuts = _top_level_commas(body)
    parts = (
        body[start + 1 : end] for start, end in zip([-1, *cuts], [*cuts, len(body)], strict=True)
    )
    return [part for part in parts if part.strip()]


def _top_level_commas(body: str) -> list[int]:
    depths = list(accumulate((char in _OPEN) - (char in _CLOSE) for char in body))
    return [index for index, char in enumerate(body) if char == "," and depths[index] == 0]


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    args = build_parser(__doc__, with_paths=False).parse_args(argv)
    return report_findings("secret_rules", run(args.root))


if __name__ == "__main__":
    sys.exit(main())
