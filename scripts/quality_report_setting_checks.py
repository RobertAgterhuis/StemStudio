"""Quality report setting check (feature 020, owner decision 2026-11-23: gate G1 failed).

The stem quality report ships switched off: ``docker-compose.yml`` passes
``StemStudio__QualityReport__Enabled`` to ``api`` and ``web`` with the default ``false``, and only
the e2e override (``docker-compose.e2e.yml``, used by the E2E, visual and a11y stacks) switches it
on. Static checks, one line each, exit 1 when any fails:

1. ``api`` and ``web`` both carry the setting (a missing one would fall back to the code default
   on one side only);
2. both have the expected value (``--expect false`` for the base file, ``true`` with the override);
3. no other service carries it.

Input: the resolved ``docker compose config --format json`` (file or ``-`` for stdin). The output
names only this setting's values. Used by ``scripts/check-quality-report-setting.{sh,ps1}``.
Standard library only.
"""

import argparse
import json
import sys
from pathlib import Path
from typing import Any

SETTING = "StemStudio__QualityReport__Enabled"
SERVICES = ("api", "web")

Config = dict[str, Any]


def _services(config: Config) -> dict[str, Any]:
    services = config.get("services")
    return services if isinstance(services, dict) else {}


def setting_of(service: Any) -> str | None:
    """Return the setting of one resolved service, lower case, or None when it is absent."""
    environment = service.get("environment") if isinstance(service, dict) else None
    if not isinstance(environment, dict) or environment.get(SETTING) is None:
        return None
    return str(environment[SETTING]).strip().lower()


def check_present(config: Config) -> str | None:
    """Return a problem when ``api`` or ``web`` lacks the setting."""
    services = _services(config)
    missing = [name for name in SERVICES if setting_of(services.get(name)) is None]
    return f"no {SETTING}: {', '.join(missing)}" if missing else None


def check_value(config: Config, expected: str) -> str | None:
    """Return a problem when ``api`` or ``web`` has another value than ``expected``."""
    services = _services(config)
    found = {name: setting_of(services.get(name)) for name in SERVICES}
    wrong = [f"{name}={value}" for name, value in found.items() if value != expected]
    return f"expected {expected}, found {'; '.join(wrong)}" if wrong else None


def check_only_api_and_web(config: Config) -> str | None:
    """Return a problem when a service other than ``api`` and ``web`` carries the setting."""
    others = sorted(
        name
        for name, service in _services(config).items()
        if name not in SERVICES and setting_of(service) is not None
    )
    return f"also set in: {', '.join(others)}" if others else None


def run_checks(config: Config, expected: str) -> list[tuple[str, str | None]]:
    """Run every check; return (name, problem or None) in order."""
    return [
        ("api and web carry the quality report setting", check_present(config)),
        (f"api and web have it {expected}", check_value(config, expected)),
        ("no other service carries it", check_only_api_and_web(config)),
    ]


def _load_config(source: str) -> Config:
    text = sys.stdin.read() if source == "-" else Path(source).read_text(encoding="utf-8")
    config = json.loads(text)
    if not isinstance(config, dict):
        raise SystemExit("the compose config is not a JSON object")
    return config


def _report(results: list[tuple[str, str | None]]) -> int:
    """Print one line per check; return 1 when any check failed."""
    for name, problem in results:
        sys.stdout.write(f"FAIL {name}: {problem}\n" if problem else f"ok   {name}\n")
    return 1 if any(problem for _, problem in results) else 0


def main(argv: list[str] | None = None) -> int:
    """Parse arguments, run every check, print one line each; return 1 when any failed."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--config", required=True, help="docker compose config JSON, - = stdin")
    parser.add_argument("--expect", required=True, choices=["true", "false"])
    args = parser.parse_args(argv)
    return _report(run_checks(_load_config(args.config), args.expect))


if __name__ == "__main__":
    sys.exit(main())
