"""Builds the full backend-api 1.23.0 = 022's full 1.22.0 + the 023 changes (DELTA.md section 5).

Usage (repo root)::

    python -I specs/023-lyric-writing-assistant/contracts/merge_full.py [--check]

023 was designed on 012's 1.11.0 (``BASE``); its Phase 0 document (``THEIRS``, git ``r22-p0-023``) is
1.11.0 + the 023 delta + the base-schema additions of DELTA.md section 1. At the 022+023 round start the
architect re-based it onto 022's full 1.22.0 (``OURS``, itself 013's 1.12.0 + 022) with this block-level
three-way merge. ``THEIRS`` is kept as ``backend-api.023-on-1.11.0.openapi.yaml`` so the merge can be
repeated and checked.

Rules (blocks = tags by name, paths, and the children of each ``components`` section):

1. A block 023 did not touch (``THEIRS`` == ``BASE``) keeps the ``OURS`` text byte for byte.
2. A block 023 added is appended at the end of its section, in 023's order; a block 023 changed
   replaces the ``OURS`` text when 022/013 did not change it. A block both sides changed differently,
   a name both sides added differently, or a block 023 removed fails the merge loudly.
3. ``info``: ``OURS`` with ``version`` 1.23.0, ``x-stemstudio-contains`` + 1.23.0 and 023's changelog
   paragraph (its "Base:" sentence re-worded) before ``license``. ``openapi``, ``servers`` and
   ``security`` must be equal on all sides.

``--check`` rebuilds in memory and fails when ``OUT`` differs (freshness) or when the result is not a
superset of ``OURS`` and of ``THEIRS`` (``info`` aside). Requires PyYAML. Exit code 0 = success.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from typing import Any

import yaml

HERE = Path(__file__).resolve().parent
BASE = Path("specs/012-practice-tempo/contracts/backend-api.openapi.yaml")
OURS = Path("specs/022-library-folders/contracts/backend-api.openapi.yaml")
THEIRS = HERE / "backend-api.023-on-1.11.0.openapi.yaml"
OUT = HERE / "backend-api.openapi.yaml"
LABEL = "1.23.0"
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")
# Base schemas one side extends (023 changelog; 022 `Asset.placement`): lists (`enum`, `required`) may
# only grow (old values kept in order) and descriptions may be re-worded; nothing else may change.
EXTENDED = {"LyricsSheet", "LyricsInput", "LyricsSnapshot", "PageContext", "DecisionReason", "JobPhase", "Asset"}
OLD_BASE = re.compile(r"Base: 1\.11\.0 \(012\).*?section 5\)\.", re.S)
NEW_BASE = (
    "Base: 1.22.0 (022 = 1.12.0 of 013 + 022), re-based at the 022+023 round start with\n"
    "      `merge_full.py` (023 DELTA.md section 5)."
)


def section_bounds(lines: list[str], header: str) -> tuple[int, int]:
    """(start, end) of the section whose header line is ``header``; ``end`` is exclusive."""
    start = lines.index(header)
    level = len(header) - len(header.lstrip())
    end = start + 1
    while end < len(lines) and (
        not lines[end].strip() or len(lines[end]) - len(lines[end].lstrip()) > level
    ):
        end += 1
    return start, end


def blocks(lines: list[str], header: str, pattern: str) -> dict[str, list[str]]:
    """The child blocks of a section, keyed by the first group of ``pattern``."""
    start, end = section_bounds(lines, header)
    found: dict[str, list[str]] = {}
    name = ""
    regex = re.compile(pattern)
    for line in lines[start + 1 : end]:
        match = regex.match(line)
        if match:
            name = match.group(1).strip("'\"")
            found[name] = []
        found[name].append(line)
    return found


def merge_section(
    where: str, base: dict[str, list[str]], ours: dict[str, list[str]], theirs: dict[str, list[str]]
) -> list[str]:
    """The merged blocks of one section (rules 1 and 2)."""
    removed = set(base) - set(theirs)
    if removed:
        raise SystemExit(f"{where}: 023 removed {sorted(removed)}")
    merged = dict(ours)
    for name, block in theirs.items():
        if name in base and block == base[name]:
            continue
        if name not in merged:
            merged[name] = block
        elif merged[name] == block:
            continue
        elif name in base and merged[name] == base[name]:
            merged[name] = block
        else:
            raise SystemExit(f"{where}: {name} changed on both sides")
    return [line for block in merged.values() for line in block]


def paragraph(base: list[str], theirs: list[str]) -> list[str]:
    """023's changelog paragraph: the info lines 023 inserted before ``license``."""
    at = theirs.index("  license:")
    first = next(i for i, (a, b) in enumerate(zip(base, theirs)) if a != b and not a.startswith("  version") and not a.startswith("  x-stemstudio"))
    text = "\n".join(theirs[first:at])
    return OLD_BASE.sub(NEW_BASE, text).split("\n")


def merged_info(ours: list[str], added: list[str]) -> list[str]:
    """Info of ``OURS`` with the new version, contained labels and 023's paragraph."""
    out: list[str] = []
    for line in ours:
        if line.startswith("  version: "):
            line = f"  version: {LABEL}"
        elif line.startswith("  x-stemstudio-contains: ["):
            line = line[:-1] + f", {LABEL}]"
        elif line == "  license:":
            out += added
        out.append(line)
    return out


def replace(lines: list[str], header: str, body: list[str]) -> list[str]:
    """``lines`` with the body of section ``header`` replaced."""
    start, end = section_bounds(lines, header)
    return lines[: start + 1] + body + lines[end:]


def merge(base_text: str, ours_text: str, theirs_text: str) -> str:
    """The merged document text."""
    base, ours, theirs = (t.split("\n") for t in (base_text, ours_text, theirs_text))
    for header in ("openapi: 3.1.0", "servers:"):
        parts = [lines[slice(*section_bounds(lines, header))] for lines in (base, ours, theirs)]
        if not parts[0] == parts[1] == parts[2]:
            raise SystemExit(f"{header} differs")
    info = [lines[slice(*section_bounds(lines, "info:"))] for lines in (base, ours, theirs)]
    out = list(ours)
    start, end = section_bounds(out, "info:")
    out[start:end] = merged_info(info[1], paragraph(info[0], info[2]))
    tag = r"^  - name: (\S+)"
    out = replace(out, "tags:", merge_section("tags", *(blocks(x, "tags:", tag) for x in (base, out, theirs))))
    path = r"^  ([^ #][^:]*):"
    out = replace(out, "paths:", merge_section("paths", *(blocks(x, "paths:", path) for x in (base, out, theirs))))
    child = r"^    ([^ #][^:]*):"
    for section in SECTIONS:
        header = f"  {section}:"
        body = merge_section(section, *(blocks(x, header, child) for x in (base, out, theirs)))
        out = replace(out, header, body)
    return "\n".join(out)


def grows(old: Any, new: Any) -> bool:
    """True when ``new`` equals ``old`` or only adds mapping keys (recursively)."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key in new and grows(value, new[key]) for key, value in old.items())
    return old == new


def extends(old: Any, new: Any) -> bool:
    """``grows`` for the ``EXTENDED`` schemas: lists may gain values, descriptions may change."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key == "description" or (key in new and extends(value, new[key])) for key, value in old.items())
    if isinstance(old, list) and isinstance(new, list):
        rest = iter(new)
        return all(any(item == other for other in rest) for item in old)
    return old == new


def lost(older: dict[str, Any], newer: dict[str, Any]) -> list[str]:
    """Entries of ``older`` that ``newer`` lost or changed (``info`` aside)."""
    found = [f"paths {n}" for n, v in older["paths"].items() if not grows(v, newer["paths"].get(n))]
    found += [f"tags {t['name']}" for t in older["tags"] if t not in newer["tags"]]
    for section in SECTIONS:
        for name, value in older["components"].get(section, {}).items():
            rule = extends if section == "schemas" and name in EXTENDED else grows
            if not rule(value, newer["components"].get(section, {}).get(name)):
                found.append(f"{section} {name}")
    return found


def check(ours_text: str, theirs_text: str, merged_text: str) -> list[str]:
    """Problems of a merged document: not a superset of ``OURS`` (1.22.0) or of ``THEIRS``."""
    merged = yaml.safe_load(merged_text)
    problems = [f"1.22.0: {p}" for p in lost(yaml.safe_load(ours_text), merged)]
    problems += [f"023: {p}" for p in lost(yaml.safe_load(theirs_text), merged)]
    contains = merged["info"]["x-stemstudio-contains"]
    if merged["info"]["version"] != LABEL or contains[-2:] != ["1.22.0", LABEL]:
        problems.append("info: version or x-stemstudio-contains")
    return problems


def main(argv: list[str]) -> int:
    """Entry point."""
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    texts = [p.read_text(encoding="utf-8") for p in (BASE, OURS, THEIRS)]
    merged_text = merge(*texts)
    problems = check(texts[1], texts[2], merged_text)
    if args.check and OUT.read_text(encoding="utf-8") != merged_text:
        problems.append(f"{OUT} is not the merge (run without --check)")
    if not args.check and not problems:
        OUT.write_text(merged_text, encoding="utf-8", newline="\n")
    for problem in problems:
        print(problem)
    print("superset: yes" if not problems else "superset: NO")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
