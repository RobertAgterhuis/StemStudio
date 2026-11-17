"""Builds the full backend-api 1.22.0 = the newest full document + the 022 delta (DELTA.md section 1).

Usage (repo root)::

    python -I specs/022-library-folders/contracts/merge_full.py [--base BASE] [--out OUT] [--check]

Defaults: ``BASE`` = specs/012-practice-tempo/contracts/backend-api.openapi.yaml (1.11.0, the newest
full document on ``main`` at 022 Phase 0), ``OUT`` = specs/022-library-folders/contracts/backend-api.openapi.yaml.
When 013 (1.12.0) is merged into the round branch, the CTO re-runs it with
``--base specs/013-library-care/contracts/backend-api.openapi.yaml`` (the merge is mechanical: the 022
names are disjoint from 013's). ``--check`` rebuilds in memory and fails when ``OUT`` differs (freshness)
or when the result is not a superset of ``BASE`` and of the delta.

The merge is textual so the base keeps its formatting byte for byte:

1. The delta's tag, paths and components are appended to the base sections; the delta's external
   ``$ref``s to the 011 document become local. ``TrackId``, ``AssetId`` (parameters) and ``NotFound``
   (response) already exist in the base with the same meaning and are not added again.
2. ``Asset`` gains the required ``placement`` member; every base example of an ``Asset`` gains a
   ``placement`` (computed with the R-02/R-03 key and folder-id rules) so it stays valid.
3. ``info.version`` = 1.22.0, ``x-stemstudio-contains`` + 1.22.0, one changelog paragraph.

Requires PyYAML (for the checks only). Exit code 0 = success.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import sys
import unicodedata
from pathlib import Path
from typing import Any

import yaml

HERE = Path(__file__).resolve().parent
DEFAULT_BASE = Path("specs/012-practice-tempo/contracts/backend-api.openapi.yaml")
DEFAULT_OUT = HERE / "backend-api.openapi.yaml"
DELTA = HERE / "backend-api.library-folders.openapi.yaml"
LABEL = "1.22.0"
EXTERNAL_REF = "'../../011-ai-composer-agent/contracts/backend-api.openapi.yaml#/"
SHARED = {
    "parameters": {"TrackId", "AssetId"},
    "responses": {"NotFound"},
    "schemas": {"Asset"},
}
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")
CHANGELOG = """\
      1.22.0 (additive, feature 022; ADR-036) - library by artist, album and song: tag `library`
      with the folder view (`GET /api/library/folders`, `GET /api/library/folders/{folderId}`:
      derived artist/album/song folders with art, counts, sort, paging, `ETag`/`304`,
      `404 folder-not-found`), the live search (`GET /api/library/search`: grouped hits with
      paths and UTF-16 highlight ranges) and library info of a track and of an item
      (`GET`/`PUT`/`DELETE /api/tracks/{trackId}/library-info` and
      `/api/assets/{assetId}/library-info`; `409 not-fileable` for recorded takes); the required
      response member `Asset.placement` (null only for recorded takes; the `Asset` examples carry
      one). `GET /api/assets` keeps its 1.5.0 semantics. Merged unchanged from
      `backend-api.library-folders.openapi.yaml` 1.22.0-delta.1; the shared `TrackId`, `AssetId`
      and `NotFound` keep their base definitions. No other entry changes. Merge order (ROADMAP
      "Full-document labels after an out-of-order merge"): this document is the newest full document
      plus the 022 delta and carries 022's allocated label 1.22.0 (`merge_full.py`).
"""


def child_blocks(
    lines: list[str], header: str, indent: int
) -> tuple[int, int, dict[str, list[str]]]:
    """Returns (start, end, blocks) of the section ``header`` (a full line) with children at ``indent`` spaces."""
    start = lines.index(header)
    level = len(header) - len(header.lstrip())
    end = start + 1
    while end < len(lines) and (
        not lines[end].strip() or len(lines[end]) - len(lines[end].lstrip()) > level
    ):
        end += 1
    blocks: dict[str, list[str]] = {}
    name = ""
    pattern = re.compile(rf"^ {{{indent}}}([^ #][^:]*):")
    for line in lines[start + 1 : end]:
        match = pattern.match(line)
        if match:
            name = match.group(1).strip("'\"")
            blocks[name] = []
        blocks[name].append(line)
    return start, end, blocks


def library_key(name: str, *, strip_the: bool) -> str:
    """The R-02 key for the simple names of the examples (accents, case, whitespace, leading "The")."""
    text = unicodedata.normalize("NFKD", name)
    text = unicodedata.normalize(
        "NFKC", "".join(c for c in text if unicodedata.category(c) != "Mn")
    )
    text = " ".join(text.lower().split())
    return text[4:] if strip_the and text.startswith("the ") else text


def folder_id(prefix: str, *keys: str) -> str:
    """The R-03 folder id: prefix + 16 hex of SHA-256 over ``prefix \\x1f key (\\x1f key)*`` (UTF-8)."""
    digest = hashlib.sha256("\x1f".join((prefix, *keys)).encode("utf-8")).hexdigest()
    return prefix + digest[:16]


def placement_lines(artist: str, song: str) -> list[str]:
    """The ``placement`` of an example item: artist and song from the import, album unknown."""
    keys = (
        library_key(artist, strip_the=True),
        library_key("Unknown album", strip_the=True),
        library_key(song, strip_the=False),
    )
    levels = (
        ("artist", folder_id("ar", keys[0]), artist, "import"),
        ("album", folder_id("al", keys[0], keys[1]), "Unknown album", "fallback"),
        ("song", folder_id("so", *keys), song, "import"),
    )
    out = ["        placement:"]
    for level, identifier, name, source in levels:
        out += [
            f"          {level}:",
            f"            folderId: {identifier}",
            f"            name: {name}",
            f"            source: {source}",
        ]
    return [*out, "          year: null", "          filed: true"]


def asset_examples(base: str, examples: dict[str, list[str]]) -> list[str]:
    """Names of the base examples whose value is an ``Asset`` (it has every 1.11.0 member)."""
    required = set(yaml.safe_load(base)["components"]["schemas"]["Asset"]["required"])
    loaded = yaml.safe_load(base)["components"]["examples"]
    return [
        name
        for name in examples
        if isinstance(loaded[name].get("value"), dict)
        and required <= set(loaded[name]["value"])
    ]


def with_placement(block: list[str]) -> list[str]:
    """An ``Asset`` example block with ``placement`` inserted before ``sizeBytes``."""
    value = yaml.safe_load("\n".join(block))
    provenance = next(iter(value.values()))["value"]["provenance"]
    at = block.index(
        next(line for line in block if line.startswith("        sizeBytes:"))
    )
    return (
        block[:at]
        + placement_lines(provenance["trackArtist"], provenance["trackTitle"])
        + block[at:]
    )


def with_placement_member(block: list[str], member: list[str]) -> list[str]:
    """The base ``Asset`` schema with ``placement`` required and in ``properties`` before ``sizeBytes``."""
    out = [
        line.replace("provenance, sizeBytes", "provenance, placement, sizeBytes")
        if "required:" in line
        else line
        for line in block
    ]
    at = out.index("        sizeBytes:")
    return out[:at] + member + out[at:]


def delta_member(delta_asset: list[str]) -> list[str]:
    """The ``placement`` property lines of the delta's ``Asset``."""
    start = delta_asset.index("        placement:")
    end = start + 1
    while delta_asset[end].startswith("          "):
        end += 1
    return delta_asset[start:end]


def merged_info(lines: list[str]) -> list[str]:
    """Info with the new version, the contained labels and the changelog paragraph."""
    out = []
    for line in lines:
        if line.startswith("  version: "):
            line = f"  version: {LABEL}"
        elif line.startswith("  x-stemstudio-contains: ["):
            line = line[:-1] + f", {LABEL}]"
        elif line == "  license:":
            out += CHANGELOG.rstrip("\n").split("\n")
        out.append(line)
    return out


def merge(base_text: str, delta_text: str) -> str:
    """The merged document text."""
    base = base_text.split("\n")
    delta = delta_text.replace(EXTERNAL_REF, "'#/").split("\n")
    out = merged_info(base)

    tag_start = out.index("  - name: health")
    library_tag = delta[delta.index("tags:") + 1 : delta.index("paths:")]
    out[tag_start:tag_start] = library_tag

    _, end, paths = child_blocks(out, "paths:", 2)
    _, _, new_paths = child_blocks(delta, "paths:", 2)
    collisions = set(paths) & set(new_paths)
    if collisions:
        raise SystemExit(f"path collision: {sorted(collisions)}")
    out[end:end] = [line for block in new_paths.values() for line in block]

    for section in SECTIONS:
        start, end, existing = child_blocks(out, f"  {section}:", 4)
        _, _, added = child_blocks(delta, f"  {section}:", 4)
        shared = SHARED.get(section, set())
        collisions = (set(existing) & set(added)) - shared
        missing = shared - set(existing)
        if collisions or missing:
            raise SystemExit(
                f"{section}: collisions {sorted(collisions)}, shared names missing in the base {sorted(missing)}"
            )
        if section == "schemas":
            existing["Asset"] = with_placement_member(
                existing["Asset"], delta_member(added["Asset"])
            )
        if section == "examples":
            for name in asset_examples(base_text, existing):
                existing[name] = with_placement(existing[name])
        body = [line for block in existing.values() for line in block]
        body += [
            line
            for name, block in added.items()
            if name not in shared
            for line in block
        ]
        out[start + 1 : end] = body
    return "\n".join(out)


def grows(old: Any, new: Any) -> bool:
    """True when ``new`` equals ``old`` or only adds mapping keys (recursively)."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key in new and grows(value, new[key]) for key, value in old.items())
    return old == new


def superset_problems(
    older: dict[str, Any], newer: dict[str, Any], skip: dict[str, set[str]]
) -> list[str]:
    """Entries of ``older`` that ``newer`` lost or changed (adding keys is allowed)."""
    found = [
        f"paths {name}"
        for name, value in older["paths"].items()
        if not grows(value, newer["paths"].get(name))
    ]
    for section in SECTIONS:
        for name, value in older["components"].get(section, {}).items():
            if name not in skip.get(section, set()) and not grows(
                value, newer["components"].get(section, {}).get(name)
            ):
                found.append(f"{section} {name}")
    return found


def check(base_text: str, delta_text: str, merged_text: str) -> list[str]:
    """Problems of a merged document: not a superset of the base or of the delta."""
    base, merged = yaml.safe_load(base_text), yaml.safe_load(merged_text)
    delta = yaml.safe_load(delta_text.replace(EXTERNAL_REF, "'#/"))
    asset = merged["components"]["schemas"]["Asset"]
    problems = (
        []
        if asset["required"][asset["required"].index("provenance") + 1] == "placement"
        else ["Asset: placement not required"]
    )
    relaxed = yaml.safe_load(merged_text)
    relaxed["components"]["schemas"]["Asset"]["required"].remove("placement")
    problems += superset_problems(base, relaxed, {})
    problems += [f"delta: {p}" for p in superset_problems(delta, merged, SHARED)]
    if (
        merged["info"]["version"] != LABEL
        or merged["info"]["x-stemstudio-contains"][-1] != LABEL
    ):
        problems.append("info: version or x-stemstudio-contains")
    return problems


def main(argv: list[str]) -> int:
    """Entry point."""
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--base", type=Path, default=DEFAULT_BASE)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    base_text = args.base.read_text(encoding="utf-8")
    delta_text = DELTA.read_text(encoding="utf-8")
    merged_text = merge(base_text, delta_text)
    problems = check(base_text, delta_text, merged_text)
    if args.check and args.out.read_text(encoding="utf-8") != merged_text:
        problems.append(
            f"{args.out} is not the merge of {args.base} and the delta (run without --check)"
        )
    if not args.check and not problems:
        args.out.write_text(merged_text, encoding="utf-8", newline="\n")
    for problem in problems:
        print(problem)
    print("superset: yes" if not problems else "superset: NO")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
