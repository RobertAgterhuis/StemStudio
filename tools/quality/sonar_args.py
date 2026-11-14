"""Translate ``sonar-project.properties`` into ``/d:`` arguments for ``dotnet sonarscanner begin``.

SonarScanner for .NET does not support ``sonar.sources``/``sonar.tests``, and ``sonar.projectKey``
is passed as ``/k:``, so those keys are left out. Secrets are never part of the file, so none
appear in the output.

Usage::

    dotnet sonarscanner begin /k:stemstudio $(python tools/quality/sonar_args.py)
"""

import sys
from collections.abc import Sequence
from pathlib import Path

from common import build_parser

SKIPPED_KEYS = frozenset({"sonar.sources", "sonar.tests", "sonar.projectKey"})
"""Keys the .NET scanner does not accept as ``/d:`` arguments."""


def parse_properties(text: str) -> dict[str, str]:
    """Parse ``key=value`` lines; ``#``/``!`` comments and blank lines are ignored."""
    pairs: dict[str, str] = {}
    for raw in text.splitlines():
        line = raw.strip()
        if line and not line.startswith(("#", "!")) and "=" in line:
            key, value = line.split("=", 1)
            pairs[key.strip()] = value.strip()
    return pairs


def to_begin_args(properties: dict[str, str]) -> list[str]:
    """Return one ``/d:key=value`` argument per supported property."""
    return [f"/d:{key}={value}" for key, value in properties.items() if key not in SKIPPED_KEYS]


def main(argv: Sequence[str] | None = None) -> int:
    """Print the begin arguments, one per line."""
    args = build_parser(__doc__, with_paths=False).parse_args(argv)
    path = Path(args.root) / "sonar-project.properties"
    properties = parse_properties(path.read_text(encoding="utf-8"))
    sys.stdout.write("\n".join(to_begin_args(properties)) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
