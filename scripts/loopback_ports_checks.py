"""Loopback ports check (refactor 004, 003 step 4, ADR-027 D3, threat T7).

The ``api`` (8081) and ``web`` (8080) services publish their ports on one host address, by
default ``127.0.0.1``, so only this computer reaches them. ``STEMSTUDIO_BIND_ADDRESS`` opens them
to a network on purpose. Static checks, one line each, exit 1 when any fails:

1. ``api`` and ``web`` each publish at least one port (so the check is never vacuous);
2. every published port of ``api`` and ``web`` binds to the expected host address (``--expect``,
   default ``127.0.0.1``); a port without ``host_ip`` binds to every interface and fails;
3. no other service publishes a port at all (the worker and the generative services are internal).

Input: the resolved ``docker compose config --format json`` (file or ``-`` for stdin). Used by
``scripts/check-loopback-ports.{sh,ps1}``, which run it once with ``STEMSTUDIO_BIND_ADDRESS``
unset and once with ``0.0.0.0`` (the opt-in is honoured). Standard library only.
"""

import argparse
import json
import sys
from collections.abc import Callable, Iterable
from pathlib import Path
from typing import Any

PUBLISHED = ("api", "web")
DEFAULT_ADDRESS = "127.0.0.1"
ALL_INTERFACES = "<all interfaces>"

Config = dict[str, Any]


def _services(config: Config) -> dict[str, Any]:
    """Return the services mapping; anything else (a list, nothing) counts as no services."""
    found = config.get("services") or {}
    return found if isinstance(found, dict) else {}


def _ports(config: Config, service: str) -> list[dict[str, Any]]:
    """Return a service's published ports (long syntax, as ``docker compose config`` emits it)."""
    definition = _services(config).get(service) or {}
    return [port for port in definition.get("ports") or [] if isinstance(port, dict)]


def _describe(service: str, port: dict[str, Any]) -> str:
    host_ip = port.get("host_ip") or ALL_INTERFACES
    return f"{service}:{port.get('published')} on {host_ip}"


def _all_ports(config: Config, services: Iterable[str]) -> list[str]:
    """Describe every published port of the given services."""
    return [_describe(name, port) for name in services for port in _ports(config, name)]


def check_publishes(config: Config) -> str | None:
    """Return a problem when ``api`` or ``web`` is missing or publishes no port."""
    silent = [name for name in PUBLISHED if not _ports(config, name)]
    return f"no published port: {', '.join(silent)}" if silent else None


def check_bind_address(config: Config, expected: str) -> str | None:
    """Return a problem for every ``api``/``web`` port that is not bound to ``expected``."""
    wrong = [
        _describe(name, port)
        for name in PUBLISHED
        for port in _ports(config, name)
        if port.get("host_ip") != expected
    ]
    return f"expected {expected}, found {'; '.join(wrong)}" if wrong else None


def check_internal_services(config: Config) -> str | None:
    """Return a problem when a service other than ``api`` and ``web`` publishes a port."""
    others = [name for name in _services(config) if name not in PUBLISHED]
    exposed = _all_ports(config, others)
    return f"internal services publish ports: {'; '.join(exposed)}" if exposed else None


def run_checks(config: Config, expected: str) -> list[tuple[str, str | None]]:
    """Run every check; return ``(name, problem or None)`` in a fixed order."""
    checks: tuple[tuple[str, Callable[[], str | None]], ...] = (
        ("api and web publish a port", lambda: check_publishes(config)),
        (f"api and web ports on {expected}", lambda: check_bind_address(config, expected)),
        ("no other service publishes a port", lambda: check_internal_services(config)),
    )
    return [(name, check()) for name, check in checks]


def report(results: Iterable[tuple[str, str | None]]) -> int:
    """Print one line per check; return 0 when all passed, else 1."""
    status = 0
    for name, problem in results:
        line = f"ok   {name}" if problem is None else f"FAIL {name}: {problem}"
        sys.stdout.write(line + "\n")
        status |= problem is not None
    return status


def _load(source: str) -> Config:
    text = sys.stdin.read() if source == "-" else Path(source).read_text(encoding="utf-8")
    loaded = json.loads(text)
    return loaded if isinstance(loaded, dict) else {}


def main(argv: list[str] | None = None) -> int:
    """Command-line entry point."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--config", required=True, help="resolved compose config JSON, - = stdin")
    parser.add_argument("--expect", default=DEFAULT_ADDRESS, help="expected host address")
    args = parser.parse_args(argv)
    return report(run_checks(_load(args.config), args.expect))


if __name__ == "__main__":
    sys.exit(main())
