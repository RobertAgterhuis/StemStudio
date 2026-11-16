"""Secret scan for the Claude key (feature 011, SC-006; constitution 1.3.0).

Fails when a file of the repository contains an Anthropic key, assigns a credential-like value to
ANTHROPIC_API_KEY, or when a CI workflow sets that variable. The key is a pass-through from the
user's shell only: compose lists it as a bare name and CI never sets it.

Usage (repository root):
    python scripts/secret_scan.py               scan the git-tracked and untracked, not ignored files
    python scripts/secret_scan.py --root DIR    scan every file under DIR (no git)
    python scripts/secret_scan.py --self-test   prove the rules fire on planted samples, then exit
Exit codes: 0 clean, 1 findings (path:line and rule; the matched value is never printed), 2 usage.
Stdlib only, so CI can run it with the runner's python3.
"""

from __future__ import annotations

import os
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

# Built from parts so this file does not match its own rule.
KEY_PREFIX = "sk" + "-ant-"
VARIABLE = "ANTHROPIC_" + "API_KEY"
MAX_BYTES = 5 * 1024 * 1024


@dataclass(frozen=True)
class Rule:
    """One pattern; ci_only rules apply to files under .github/ only."""

    name: str
    pattern: re.Pattern[str]
    ci_only: bool = False


RULES = (
    Rule("anthropic-key", re.compile(re.escape(KEY_PREFIX) + r"[A-Za-z0-9_-]")),
    # A value of 16+ key characters; references ($VAR, ${VAR}), placeholders (<...>) and commands
    # such as Read-Host stay allowed.
    Rule(
        "key-value-assigned",
        re.compile(
            VARIABLE + r"""['"]?\s*[:=]\s*['"]?[A-Za-z0-9_-]{16,}(?=['"\s,;)]|$)"""
        ),
    ),
    Rule(
        "ci-sets-key",
        re.compile(VARIABLE + r"\s*:|secrets\.ANTHROPIC", re.IGNORECASE),
        ci_only=True,
    ),
)


@dataclass(frozen=True)
class Finding:
    """A rule match; the matched text is deliberately not kept."""

    path: str
    line: int
    rule: str


def git_files(root: Path) -> list[Path]:
    """Tracked plus untracked, not ignored files (a key about to be committed is caught too)."""
    output = subprocess.run(
        ["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
        cwd=root,
        check=True,
        capture_output=True,
    ).stdout
    return [root / name for name in output.decode("utf-8").split("\0") if name]


def walk_files(root: Path) -> list[Path]:
    """Every regular file under root."""
    return [Path(base) / name for base, _, names in os.walk(root) for name in names]


def read_text(path: Path) -> str | None:
    """File text, or None for missing, large or binary files."""
    try:
        if path.is_symlink() or path.stat().st_size > MAX_BYTES:
            return None
        data = path.read_bytes()
    except OSError:
        return None
    if b"\0" in data[:8192]:
        return None
    return data.decode("utf-8", errors="replace")


def applicable_rules(relative: str) -> tuple[Rule, ...]:
    """The rules for one file: the CI-only rules apply under .github/ only."""
    if relative.startswith(".github/"):
        return RULES
    return tuple(rule for rule in RULES if not rule.ci_only)


def scan_text(relative: str, text: str) -> list[Finding]:
    """Findings of every applicable rule in one file."""
    rules = applicable_rules(relative)
    lines = enumerate(text.splitlines(), start=1)
    return [
        Finding(relative, number, rule.name)
        for number, line in lines
        for rule in rules
        if rule.pattern.search(line)
    ]


def scan(root: Path, files: list[Path]) -> list[Finding]:
    """Findings over the given files; paths are reported relative to root with forward slashes."""
    findings: list[Finding] = []
    for path in files:
        text = read_text(path)
        if text is not None:
            findings += scan_text(path.relative_to(root).as_posix(), text)
    return findings


SELF_TEST_SAMPLES = {
    "leak/key.txt": f"token = {KEY_PREFIX}api03-{'x' * 24}\n",
    "leak/app.env": f"{VARIABLE}=abcdefghijklmnop1234\n",
    ".github/workflows/leak.yml": f"env:\n  {VARIABLE}: ${{{{ secrets.CLAUDE }}}}\n",
    "ok/compose.yml": f"environment:\n  - {VARIABLE}\n  - OTHER=${{{VARIABLE}:-}}\n",
    "ok/howto.md": f"$env:{VARIABLE} = Read-Host -MaskInput\nexport {VARIABLE}=<your key>\n",
}
SELF_TEST_EXPECTED = {
    Finding("leak/key.txt", 1, "anthropic-key"),
    Finding("leak/app.env", 1, "key-value-assigned"),
    Finding(".github/workflows/leak.yml", 2, "ci-sets-key"),
}


def plant_samples(root: Path) -> None:
    """Writes SELF_TEST_SAMPLES under root."""
    for name, content in SELF_TEST_SAMPLES.items():
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_text(content, encoding="utf-8")


def self_test() -> int:
    """Plants leaking and allowed samples in a temp dir; 0 when exactly the leaks are found."""
    with tempfile.TemporaryDirectory() as temp:
        plant_samples(Path(temp))
        found = set(scan(Path(temp), walk_files(Path(temp))))
    if found == SELF_TEST_EXPECTED:
        print(
            f"secret scan self-test passed ({len(found)} planted leaks, allowed forms clean)"
        )
        return 0
    missing, extra = SELF_TEST_EXPECTED - found, found - SELF_TEST_EXPECTED
    print(f"secret scan self-test FAILED: missing {missing}, extra {extra}")
    return 1


def report(findings: list[Finding], scanned: int) -> int:
    """Prints the findings; returns the exit code."""
    for finding in findings:
        print(
            f"::error file={finding.path},line={finding.line}::secret scan: {finding.rule}"
        )
    if findings:
        print(
            f"secret scan: {len(findings)} finding(s) in {scanned} files; remove the value, use the shell"
        )
        return 1
    print(
        f"secret scan: {scanned} files clean (no Claude key, no key value, CI does not set it)"
    )
    return 0


def main(argv: list[str]) -> int:
    """Entry point; see the module docstring."""
    if argv == ["--self-test"]:
        return self_test()
    if len(argv) == 2 and argv[0] == "--root":
        root = Path(argv[1]).resolve()
        files = walk_files(root)
    elif not argv:
        root = Path(__file__).resolve().parent.parent
        files = git_files(root)
    else:
        print(__doc__, file=sys.stderr)
        return 2
    return report(scan(root, files), len(files))


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
