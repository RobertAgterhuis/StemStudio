"""Library mount check (feature 007, FR-012, ADR-016 D5): runs INSIDE the worker container.

The worker reads the API library through a read-only mount of the ``api-data`` volume at
``/library``; the API is the only writer. Three checks, one line each, exit 1 when any fails:

1. ``STEMSTUDIO_LIBRARY_DIR`` is ``/library``;
2. ``GET /health`` reports ``library.readable: true``;
3. a write attempt inside ``/library`` as the worker user fails with "Read-only file system".

Used by ``scripts/check-library-mount.{sh,ps1}``, which pipe this file into
``docker compose exec -T worker python -I -``. Standard library only.
"""

import errno
import json
import os
import sys
import urllib.request
from collections.abc import Callable
from pathlib import Path

LIBRARY = Path("/library")
HEALTH_URL = "http://127.0.0.1:8000/health"
PROBE_NAME = ".stemstudio-write-probe"
READ_ONLY = "Read-only file system"


def check_environment() -> str | None:
    """Return a problem when ``STEMSTUDIO_LIBRARY_DIR`` is not ``/library``."""
    value = os.environ.get("STEMSTUDIO_LIBRARY_DIR")
    if value == str(LIBRARY):
        return None
    return f"STEMSTUDIO_LIBRARY_DIR is {value!r}, expected '{LIBRARY}'"


def check_health() -> str | None:
    """Return a problem when ``/health`` does not report ``library.readable: true``."""
    try:
        with urllib.request.urlopen(HEALTH_URL, timeout=5) as response:
            body = json.load(response)
    except (OSError, ValueError) as error:
        return f"GET {HEALTH_URL} failed: {error}"
    library = body.get("library")
    if isinstance(library, dict) and library.get("readable") is True:
        return None
    return f"/health library is {library!r}, expected readable: true"


def check_read_only() -> str | None:
    """Return a problem unless writing a probe file in ``/library`` fails with EROFS."""
    probe = LIBRARY / PROBE_NAME
    try:
        probe.write_bytes(b"")
    except OSError as error:
        if error.errno == errno.EROFS:
            return None
        return f"write to {LIBRARY} failed with {error.strerror!r}, expected {READ_ONLY!r}"
    probe.unlink()
    return f"write to {LIBRARY} succeeded, expected {READ_ONLY!r}"


CHECKS: tuple[tuple[str, Callable[[], str | None]], ...] = (
    ("worker STEMSTUDIO_LIBRARY_DIR", check_environment),
    ("worker /health library.readable", check_health),
    ("worker cannot write /library", check_read_only),
)


def main() -> int:
    """Run every check, print one line per check; return 1 when any check failed."""
    failed = 0
    for name, check in CHECKS:
        problem = check()
        sys.stdout.write(f"FAIL {name}: {problem}\n" if problem else f"ok   {name}\n")
        failed += problem is not None
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
