"""Copies the app's light-theme tokens and the Archivo font into the handout assets (020 T007).

Usage (from ``worker/`` or anywhere)::

    uv run python scripts/sync_handout_style.py [--check] [--root <repo root>]

Sources (single source of truth, research.md R-09): ``src/StemStudio.Web/wwwroot/css/tokens.css``
(the theme-independent ``:root`` block and the light theme block, "day session") and every file of
``src/StemStudio.Web/wwwroot/fonts/archivo/``. Copies go to
``worker/src/stemstudio_worker/handouts/assets/`` (``tokens.css``, ``fonts/archivo/*``) with a
``manifest.json`` of the SHA-256 of every source and copy. ``--check`` writes nothing and exits 1
when a copy or the manifest differs from what the sources give (drift; also run by the
quality gate). The handout CSS uses only ``var(--ss-*)`` tokens from this copy (FR-012).
"""

import argparse
import hashlib
import json
import re
import sys
from collections.abc import Sequence
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
TOKENS = Path("src/StemStudio.Web/wwwroot/css/tokens.css")
FONTS = Path("src/StemStudio.Web/wwwroot/fonts/archivo")
ASSETS = Path("worker/src/stemstudio_worker/handouts/assets")
SHARED_BLOCK = ":root"
LIGHT_BLOCK = 'html[data-theme="light"]'
RUN = "worker/scripts/sync_handout_style.py"
HEADER = (
    "/*\n * Handout tokens: copied by worker/scripts/sync_handout_style.py from\n"
    " * src/StemStudio.Web/wwwroot/css/tokens.css (theme-independent tokens and the light theme,\n"
    ' * "day session"). Do not edit: change the app tokens and run the script\n'
    " * (manifest.json).\n */\n"
)


class StyleSyncError(Exception):
    """A source cannot be read as expected (a token block is missing)."""


def block(css: str, selector: str) -> str:
    """Body of the first top-level rule whose selector line is exactly ``<selector> {``."""
    match = re.search(
        rf"^{re.escape(selector)} \{{\n(.*?)^\}}", css, flags=re.MULTILINE | re.DOTALL
    )
    if match is None:
        msg = f"tokens.css has no {selector!r} block"
        raise StyleSyncError(msg)
    return match.group(1)


def handout_tokens(css: str) -> str:
    """The handout ``tokens.css``: the shared block and the light block, both as ``:root``."""
    shared, light = block(css, SHARED_BLOCK), block(css, LIGHT_BLOCK)
    return (
        f"{HEADER}:root {{\n{shared}}}\n\n/* Light theme (day session). */\n:root {{\n{light}}}\n"
    )


def digest(data: bytes) -> str:
    """Hex SHA-256 of ``data``."""
    return hashlib.sha256(data).hexdigest()


def sources(root: Path) -> dict[Path, bytes]:
    """The source files (repository-relative) with their content."""
    found = {TOKENS: (root / TOKENS).read_bytes()}
    found.update({FONTS / f.name: f.read_bytes() for f in sorted((root / FONTS).iterdir())})
    return found


def manifest(read: dict[Path, bytes], copies: dict[Path, bytes]) -> bytes:
    """``manifest.json``: SHA-256 of every source and copy."""
    content = {
        "generatedBy": RUN,
        "sources": {path.as_posix(): digest(data) for path, data in read.items()},
        "copies": {path.as_posix(): digest(data) for path, data in copies.items()},
    }
    return (json.dumps(content, indent=2, sort_keys=True) + "\n").encode("utf-8")


def expected(root: Path) -> dict[Path, bytes]:
    """Every output file (relative to the assets folder) with its content."""
    read = sources(root)
    css = handout_tokens(read[TOKENS].decode("utf-8")).encode("utf-8")
    files = {Path("tokens.css"): css}
    files.update({Path("fonts/archivo") / p.name: data for p, data in read.items() if p != TOKENS})
    return files | {Path("manifest.json"): manifest(read, files)}


def drift(root: Path, files: dict[Path, bytes]) -> list[str]:
    """Output paths whose committed copy is missing or differs."""
    assets = root / ASSETS
    return [
        p.as_posix()
        for p, data in files.items()
        if not (assets / p).is_file() or (assets / p).read_bytes() != data
    ]


def write(root: Path, files: dict[Path, bytes]) -> None:
    """Write every output file (LF line endings are part of the content)."""
    for path, data in files.items():
        target = root / ASSETS / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)


def main(argv: Sequence[str]) -> int:
    """Entry point; 0 = in sync (or written), 1 = drift found by ``--check``."""
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n", 1)[0])
    parser.add_argument("--check", action="store_true", help="fail on drift, write nothing")
    parser.add_argument("--root", type=Path, default=REPO, help="repository root")
    args = parser.parse_args(argv)
    files = expected(args.root)
    if not args.check:
        write(args.root, files)
        return 0
    stale = drift(args.root, files)
    for path in stale:
        sys.stdout.write(f"handout assets out of date: {path} (run {RUN})\n")
    return 1 if stale else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
