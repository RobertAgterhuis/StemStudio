"""Key volume check (011 follow-up claude-key, T019/T020; CK-FR-003, CK-FR-005).

The API's Data Protection key ring lives in its own named volume ``api-keys`` at ``/keys``,
mounted only into ``api`` (never into the worker's read-only library mount, the Web app or the
generative services) and never part of the library or a 013 backup. Static checks, one line
each, exit 1 when any fails:

1. the top-level volume ``api-keys`` is declared;
2. ``api`` mounts it as a named volume at ``/keys``;
3. no other service mounts it, nothing else is mounted at ``/keys`` and ``api`` mounts it once;
4. ``api`` has ``StemStudio__DataProtection__KeyRingDir=/keys``;
5. ``api`` has ``StemStudio__Agent__KeyEntry__InternalCaller=web`` and the ``web`` service exists;
6. the runtime stage of ``docker/api.Dockerfile`` creates ``/keys`` with mode 0700 owned by
   ``$APP_UID``;
7. it declares ``VOLUME`` ``/keys``;
8. it runs as ``$APP_UID`` (no later ``USER``);
9. ``ANTHROPIC_API_KEY`` stays a bare pass-through in every compose file (no value assigned).

Input: the resolved ``docker compose config --format json`` (file or ``-`` for stdin), the
Dockerfile and the compose files. The output never contains an environment value except the
two checked configuration paths. Used by ``scripts/check-key-volume.{sh,ps1}``. Standard library
only.
"""

import argparse
import json
import re
import sys
from collections.abc import Callable, Iterable
from pathlib import Path
from typing import Any

VOLUME = "api-keys"
TARGET = "/keys"
API = "api"
KEY_RING_ENV = "StemStudio__DataProtection__KeyRingDir"
CALLER_ENV = "StemStudio__Agent__KeyEntry__InternalCaller"
CALLER = "web"
CLAUDE_ENV = "ANTHROPIC_API_KEY"
_UID = r'(?:"\$APP_UID"|\$APP_UID|"\$\{APP_UID\}"|\$\{APP_UID\})'
_INSTALL = re.compile(
    rf"install\s+-d\s+-m\s+0?700\s+-o\s+{_UID}\s+-g\s+{_UID}\s+(?:\S+\s+)*?/keys\b"
)
_VOLUME_LINE = re.compile(r"^\s*VOLUME\s+\[(?P<items>[^\]]*)\]", re.MULTILINE)
_USER_LINE = re.compile(r"^\s*USER\s+(?P<user>\S+)", re.MULTILINE)
_BARE_CLAUDE = re.compile(rf"^\s*-\s*{CLAUDE_ENV}\s*$", re.MULTILINE)
_ASSIGNED_CLAUDE = re.compile(rf"\b{CLAUDE_ENV}\s*[:=]")

Config = dict[str, Any]


def _services(config: Config) -> dict[str, Any]:
    services = config.get("services")
    return services if isinstance(services, dict) else {}


def _environment(config: Config, service: str) -> dict[str, str | None]:
    """Return a service's environment as a mapping (compose emits a mapping or a list)."""
    raw = _services(config).get(service, {}).get("environment") or {}
    return raw if isinstance(raw, dict) else dict(_env_entry(str(item)) for item in raw)


def _env_entry(item: str) -> tuple[str, str | None]:
    """Split a list-form entry ``NAME=value``; a bare ``NAME`` has no value."""
    name, sep, value = item.partition("=")
    return (name, value) if sep else (name, None)


def _mounts(definition: dict[str, Any]) -> list[tuple[str, str, str]]:
    """Return ``(type, source, target)`` of every volume entry (short syntax parsed too)."""
    mounts = []
    for entry in definition.get("volumes") or []:
        if isinstance(entry, str):
            source, _, rest = entry.partition(":")
            mounts.append(("volume", source, rest.partition(":")[0]))
        else:
            mounts.append((entry.get("type", ""), entry.get("source", ""), entry.get("target", "")))
    return mounts


def check_volume_declared(config: Config) -> str | None:
    """Return a problem when the top-level volume ``api-keys`` is not declared."""
    volumes = config.get("volumes") or {}
    return None if VOLUME in volumes else f"top-level volume {VOLUME!r} is not declared"


def check_api_mount(config: Config) -> str | None:
    """Return a problem unless ``api`` mounts the named volume ``api-keys`` at ``/keys``."""
    definition = _services(config).get(API)
    if definition is None:
        return f"service {API!r} is missing"
    if ("volume", VOLUME, TARGET) in _mounts(definition):
        return None
    return f"{API} does not mount the named volume {VOLUME!r} at {TARGET}"


def _key_ring_mounts(definition: dict[str, Any]) -> list[tuple[str, str]]:
    """Return ``(source, target)`` of every mount of the key ring volume or at ``/keys``."""
    return [(s, t) for _, s, t in _mounts(definition) if s == VOLUME or t == TARGET]


def _foreign_mounts(name: str, definition: dict[str, Any]) -> list[str]:
    """Return the problems of one service: a key-ring mount outside ``api``, or a second one."""
    touching = _key_ring_mounts(definition)
    if name != API:
        return [f"{name} mounts {source!r} at {target}" for source, target in touching]
    return [f"{API} mounts {VOLUME} {len(touching)} times"] if len(touching) > 1 else []


def check_only_api_mounts(config: Config) -> str | None:
    """Return a problem when a service other than ``api`` sees the key ring volume or ``/keys``."""
    problems = [p for name, d in _services(config).items() for p in _foreign_mounts(name, d or {})]
    return "; ".join(problems) or None


def check_key_ring_env(config: Config) -> str | None:
    """Return a problem unless ``api`` sets ``StemStudio__DataProtection__KeyRingDir=/keys``."""
    if API not in _services(config):
        return f"service {API!r} is missing"
    value = _environment(config, API).get(KEY_RING_ENV)
    return None if value == TARGET else f"{KEY_RING_ENV} is {value!r}, expected {TARGET!r}"


def check_internal_caller_env(config: Config) -> str | None:
    """Return a problem unless ``api`` names ``web`` as internal caller and ``web`` exists."""
    value = _environment(config, API).get(CALLER_ENV)
    if value != CALLER:
        return f"{CALLER_ENV} is {value!r}, expected {CALLER!r}"
    return None if CALLER in _services(config) else f"service {CALLER!r} is missing"


def _runtime_stage(dockerfile: str) -> str:
    """Return the text of the last build stage (the runtime image)."""
    return re.split(r"^\s*FROM\s", dockerfile, flags=re.MULTILINE | re.IGNORECASE)[-1]


def check_dockerfile_keys_dir(dockerfile: str) -> str | None:
    """Return a problem unless the runtime stage creates ``/keys`` 0700 for ``$APP_UID``."""
    if _INSTALL.search(_runtime_stage(dockerfile)):
        return None
    return 'runtime stage lacks: install -d -m 0700 -o "$APP_UID" -g "$APP_UID" /keys'


def check_dockerfile_volume(dockerfile: str) -> str | None:
    """Return a problem unless the runtime stage declares ``VOLUME`` with ``/keys``."""
    for match in _VOLUME_LINE.finditer(_runtime_stage(dockerfile)):
        if f'"{TARGET}"' in match.group("items"):
            return None
    return f'runtime stage lacks VOLUME ["{TARGET}"]'


def check_dockerfile_user(dockerfile: str) -> str | None:
    """Return a problem unless the last ``USER`` of the runtime stage is ``$APP_UID``."""
    users = [m.group("user") for m in _USER_LINE.finditer(_runtime_stage(dockerfile))]
    if users and users[-1] in ("$APP_UID", "${APP_UID}"):
        return None
    return "runtime stage does not end as USER $APP_UID"


def _code_lines(text: str) -> Iterable[str]:
    return (line for line in text.splitlines() if not line.lstrip().startswith("#"))


def _assigns_claude_key(compose_texts: list[str]) -> bool:
    """Return True when a non-comment line of a compose file gives the Claude key a value."""
    lines = (line for text in compose_texts for line in _code_lines(text))
    return any(_ASSIGNED_CLAUDE.search(line) for line in lines)


def check_claude_key_pass_through(compose_texts: list[str]) -> str | None:
    """Return a problem unless ``ANTHROPIC_API_KEY`` is a bare entry and never assigned."""
    if _assigns_claude_key(compose_texts):
        return f"a compose file assigns {CLAUDE_ENV} a value (keep the bare pass-through entry)"
    if any(_BARE_CLAUDE.search(text) for text in compose_texts):
        return None
    return f"no compose file passes {CLAUDE_ENV} through as a bare entry"


def run_checks(
    config: Config, dockerfile: str, compose_texts: list[str]
) -> list[tuple[str, str | None]]:
    """Run every check; return ``(name, problem or None)`` in a fixed order."""
    checks: tuple[tuple[str, Callable[[], str | None]], ...] = (
        (f"volume {VOLUME} declared", lambda: check_volume_declared(config)),
        (f"api mounts {VOLUME} at {TARGET}", lambda: check_api_mount(config)),
        (f"only api sees {VOLUME} and {TARGET}", lambda: check_only_api_mounts(config)),
        (f"api {KEY_RING_ENV}", lambda: check_key_ring_env(config)),
        (f"api {CALLER_ENV}", lambda: check_internal_caller_env(config)),
        ("api.Dockerfile /keys 0700 $APP_UID", lambda: check_dockerfile_keys_dir(dockerfile)),
        ("api.Dockerfile VOLUME /keys", lambda: check_dockerfile_volume(dockerfile)),
        ("api.Dockerfile USER $APP_UID", lambda: check_dockerfile_user(dockerfile)),
        (f"{CLAUDE_ENV} bare pass-through", lambda: check_claude_key_pass_through(compose_texts)),
    )
    return [(name, check()) for name, check in checks]


def _load_config(source: str) -> Config:
    text = sys.stdin.read() if source == "-" else Path(source).read_text(encoding="utf-8")
    config = json.loads(text)
    if not isinstance(config, dict):
        raise SystemExit("the compose config is not a JSON object")
    return config


def main(argv: list[str] | None = None) -> int:
    """Parse arguments, run every check, print one line each; return 1 when any failed."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--config", required=True, help="docker compose config JSON, - = stdin")
    parser.add_argument("--dockerfile", required=True, type=Path)
    parser.add_argument("--compose-file", action="append", required=True, type=Path)
    args = parser.parse_args(argv)
    texts = [path.read_text(encoding="utf-8") for path in args.compose_file]
    return _report(run_checks(_load_config(args.config), args.dockerfile.read_text("utf-8"), texts))


def _report(results: list[tuple[str, str | None]]) -> int:
    """Print one line per check; return 1 when any check failed."""
    for name, problem in results:
        sys.stdout.write(f"FAIL {name}: {problem}\n" if problem else f"ok   {name}\n")
    return 1 if any(problem for _, problem in results) else 0


if __name__ == "__main__":
    sys.exit(main())
