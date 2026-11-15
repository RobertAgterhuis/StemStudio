"""Licence report of one image's Python environment (009 T083 / 010 T061 input): runs INSIDE it.

Prints a Markdown table (name, version, licence, URL) of every installed distribution, read from
the package metadata (``License-Expression``, else ``License``, else the ``License ::``
classifiers), sorted by name, and a closing line that counts the distributions without licence
metadata. Used by ``scripts/image-licences.{sh,ps1}``, which pipe this file into
``docker run --rm -i --entrypoint python <image> -I -``. Standard library only.
"""

import importlib.metadata
import sys
from email.message import Message

UNKNOWN = "UNKNOWN"
CLASSIFIER = "License :: "
MAX_LICENCE_TEXT = 60


def licence(meta: Message) -> str:
    """Return the licence of one distribution's metadata (``UNKNOWN`` when none is declared)."""
    expression = (meta.get("License-Expression") or "").strip()
    if expression:
        return expression
    text = (meta.get("License") or "").strip().splitlines()
    if text and len(text[0]) <= MAX_LICENCE_TEXT:
        return text[0]
    classifiers = [
        c.split(" :: ")[-1] for c in meta.get_all("Classifier") or [] if c.startswith(CLASSIFIER)
    ]
    return "; ".join(classifiers) or UNKNOWN


def url(meta: Message) -> str:
    """Return the home page or the first project URL of the metadata ('' when none)."""
    home = (meta.get("Home-page") or "").strip()
    projects = [entry.split(",", 1)[-1].strip() for entry in meta.get_all("Project-URL") or []]
    return home or next(iter(projects), "")


def rows() -> list[tuple[str, str, str, str]]:
    """Return ``(name, version, licence, url)`` per installed distribution, sorted by name."""
    found = {}
    for dist in importlib.metadata.distributions():
        meta = dist.metadata
        name = meta["Name"] or ""
        found[name.lower()] = (name, meta["Version"] or "", licence(meta), url(meta))  # type: ignore[arg-type]
    return [found[key] for key in sorted(found)]


def main() -> int:
    """Print the table and the count of distributions without licence metadata."""
    table = rows()
    sys.stdout.write("| Name | Version | Licence | URL |\n|---|---|---|---|\n")
    sys.stdout.writelines(f"| {' | '.join(row)} |\n" for row in table)
    unknown = [row[0] for row in table if row[2] == UNKNOWN]
    sys.stdout.write(
        f"\n{len(table)} distributions; without licence metadata: {unknown or 'none'}\n"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
