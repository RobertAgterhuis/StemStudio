"""Builds the full 020 contracts: worker-api 1.11.0 and backend-api 1.19.0 (architect, 020 Phase 0 T003).

Usage (repo root)::

    python -I specs/020-insights-handouts/contracts/merge_full.py [--check]

* worker-api 1.11.0 = 016's full 1.9.0 (``WORKER.base``) + ``worker-api.delta.openapi.yaml``.
* ui-test-ids 1.19.0 = 016's full 1.15.0 + the "Ids" table and rules of ``ui-test-ids.delta.md`` (G2 ids deferred).
* backend-api 1.19.0 = 016's full 1.15.0 (``BACKEND.base``) + ``backend-api.delta.openapi.yaml``.

017-019 are not built yet; the labels stay as allocated (docs/ROADMAP.md "Full-document labels after an
out-of-order merge"). The merge is text-based (as 016's ``merge_full.py``) and fails loudly when an anchor
is missing or not unique:

1. The delta's paths are appended to ``paths`` except the G2 paths (tag ``reseparation``, deferred).
2. The delta's components are appended except the copies of base components (``shared``) and the
   deferred G2 components (``deferred``).
3. Base schemas grow: worker ``Health``/``LanesHealth`` gain ``insights`` (required, the worker always
   sends them; the ``/health`` examples too); backend ``AssetKind``/``AssetOrigin`` gain ``handout`` and
   ``Asset.handout``, ``Provenance.handoutRecipe``, ``Track.quality`` are added as optional properties
   (absent = null).
4. Tags ``insights``/``handouts`` before ``health``; ``info.version``, backend ``x-stemstudio-contains``
   + 1.19.0, a changelog paragraph.

``--check`` rebuilds both in memory and fails when an output differs (freshness) or is not a superset
of its base (every base entry kept; only ``extended`` entries may grow). Requires PyYAML.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

HERE = Path(__file__).resolve().parent
SPECS = HERE.parents[1]
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")
G2_PATHS = ("/v1/reseparation-jobs", "/api/tracks/{trackId}/reseparation")

WORKER_CHANGELOG = """      1.11.0 (additive, feature 020; ADR-029) - insights and handouts: a sixth lane `insights` (one
      job at a time, FIFO, BLAS threads capped by `STEMSTUDIO_INSIGHTS_THREADS`; never waits for
      `separate`, `render`, `transcribe`, `practice` or `instrument-analysis`) with quality jobs
      (`/v1/quality-jobs/...`: reference-free bleed and artefact estimates per stem and detail stem,
      levels, rating and reason code; at least the six standard stems) and handout jobs
      (`/v1/handout-jobs/...`: a resolved `HandoutDocument` laid out as one PDF, fixed notice texts
      validated, `GET .../file`; no network). Handout jobs accept only notation PDFs
      (`assets/<uuid>/<name>.pdf`) and artwork (`tracks/<uuid>/artwork/(auto|user)/cover.jpg`);
      quality jobs only stem and detail stem paths; the general allow-list is unchanged. Jobs kept
      24 h; "interrupted by restart" after a restart. `Health.insights`, `Health.lanes.insights`.
      Merged from `worker-api.delta.openapi.yaml` 1.11.0-delta.2 by `merge_full.py` onto 016's
      1.9.0; the G2 re-separation parts of the delta are deferred and not merged. See ADR-029.
"""

BACKEND_CHANGELOG = """      1.19.0 (additive, feature 020; ADR-029) - insights and handouts, tags `insights` and `handouts`:
      the stem quality report with tips (`GET`/`POST /api/tracks/{trackId}/quality`; estimates with
      reliability labels, at most two tips per stem, no GPU re-run tip), lesson handouts per family
      for a song or a setlist (`GET /api/handouts/options`, `POST /api/assets/handouts`: library items
      of kind `handout` with one `pdf` file, `Asset.handout` facts and `Provenance.handoutRecipe`) and
      practice notes per track and family (`/api/tracks/{trackId}/practice-notes[/{family}]`).
      `AssetKind` and `AssetOrigin` + `handout`; `Asset.handout`, `Provenance.handoutRecipe` and
      `Track.quality` are optional (absent = null). Merged from `backend-api.delta.openapi.yaml`
      1.19.0-delta.2 by `merge_full.py` onto 016's 1.15.0 (017-019 not built; the label stays as
      allocated); the G2 re-separation parts are deferred and not merged. No earlier entry changes
      except these additions.
"""


@dataclass(frozen=True)
class Target:
    """One full document to build."""

    base: Path
    delta: Path
    out: Path
    label: str
    previous: str
    shared: frozenset[str]
    deferred: frozenset[str]
    extended: frozenset[tuple[str, str]]


WORKER = Target(
    SPECS / "016-instrument-views/contracts/worker-api.openapi.yaml",
    HERE / "worker-api.delta.openapi.yaml",
    HERE / "worker-api.openapi.yaml",
    "1.11.0",
    "1.9.0",
    frozenset({"LibraryPath", "StemName", "ParentStemName", "RenderJobStatus", "JobDevice", "LaneHealth",
               "ProblemDetails", "NotFound", "InvalidInputPath", "InputNotFound", "ValidationFailed"}),
    frozenset({"ReseparationJobId", "SeparationPipelinesHealth", "SeparationPipeline", "ReseparationInputs",
               "ReseparationJobRequest", "ReseparationStage", "ReseparationStem", "ReseparationResult",
               "ReseparationJob", "ReseparationNotCompleted"}),
    frozenset({("schemas", "Health"), ("schemas", "LanesHealth"), ("paths", "/health")}),
)
BACKEND = Target(
    SPECS / "016-instrument-views/contracts/backend-api.openapi.yaml",
    HERE / "backend-api.delta.openapi.yaml",
    HERE / "backend-api.openapi.yaml",
    "1.19.0",
    "1.15.0",
    frozenset({"TrackId", "ChannelId", "StemName", "ParentStemName", "Asset", "ProblemDetails",
               "ValidationProblemDetails", "NotFound", "ValidationProblem", "WorkerUnavailable"}),
    frozenset({"SeparationPipeline", "StemSet", "ReseparationStatus", "ReseparationPhase", "ReseparationOptions",
               "StartReseparationRequest", "ReseparationConflict"}),
    frozenset({("schemas", n) for n in ("Track", "Asset", "Provenance", "AssetKind", "AssetOrigin")}),
)


def replace_once(text: str, old: str, new: str) -> str:
    """Replaces a unique anchor; fails when it is missing or ambiguous."""
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"anchor found {count} times: {old[:90]!r}")
    return text.replace(old, new)


def block(text: str, header: str, indent: int) -> tuple[int, int]:
    """Offsets of the body of the unique key ``header`` at ``indent`` spaces (until the next key at <= indent)."""
    match = list(re.finditer(rf"^{' ' * indent}{re.escape(header)}:\n", text, flags=re.MULTILINE))
    if len(match) != 1:
        raise SystemExit(f"{header!r} at indent {indent}: {len(match)} matches")
    start = match[0].end()
    nxt = re.search(rf"^ {{0,{indent}}}\S", text[start:], flags=re.MULTILINE)
    return start, start + nxt.start() if nxt else len(text)


def items(body: str, indent: int) -> dict[str, str]:
    """Raw text of the children of a block, by key."""
    found: dict[str, str] = {}
    for chunk in re.split(rf"(?=^{' ' * indent}\S)", body, flags=re.MULTILINE):
        if chunk.strip():
            found[chunk.split(":", 1)[0].strip().strip("'\"")] = chunk
    return found


def in_block(text: str, header: str, indent: int, old: str, new: str) -> str:
    """Replaces a unique string inside one block."""
    start, end = block(text, header, indent)
    return text[:start] + replace_once(text[start:end], old, new) + text[end:]


def add_properties(text: str, schema: str, props: str, required: bool) -> str:
    """Inserts properties first in ``properties`` of a schema; optionally appends their names to ``required``."""
    names = list(items(props, 8))
    text = in_block(text, schema, 4, "      properties:\n", "      properties:\n" + props)
    if required:
        start, end = block(text, schema, 4)
        body = re.sub(r"^      required: \[(.*)\]$", lambda m: f"      required: [{m.group(1)}, {', '.join(names)}]",
                      text[start:end], count=1, flags=re.MULTILINE)
        text = text[:start] + body + text[end:]
    return text


def add_enum(text: str, schema: str, values: list[str]) -> str:
    """Appends values to the single-line ``enum`` of a string schema."""
    start, end = block(text, schema, 4)
    body, count = re.subn(r"^      enum: \[(.*)\]$", lambda m: f"      enum: [{m.group(1)}, {', '.join(values)}]",
                          text[start:end], count=1, flags=re.MULTILINE)
    if count != 1:
        raise SystemExit(f"enum of {schema} missing")
    return text[:start] + body + text[end:]


def kept_paths(delta: str) -> str:
    """The delta's ``paths`` children without the deferred G2 paths."""
    start, end = block(delta, "paths", 0)
    return "".join(chunk for name, chunk in items(delta[start:end], 2).items() if not name.startswith(G2_PATHS))


def append_delta(text: str, delta: str, target: Target) -> str:
    """Steps 1 and 2: paths and components of the delta."""
    p_end = block(text, "paths", 0)[1]
    text = text[:p_end] + kept_paths(delta) + text[p_end:]
    c_start, c_end = block(delta, "components", 0)
    components = delta[c_start:c_end]
    for section in SECTIONS:
        if f"\n  {section}:\n" not in "\n" + components:
            continue
        s_start, s_end = block(components, section, 2)
        kept = [chunk for name, chunk in items(components[s_start:s_end], 4).items()
                if name not in target.shared and name not in target.deferred]
        at = block(text, "components", 0)[0]
        at = block(text[at:], section, 2)[1] + at
        text = text[:at] + "".join(kept) + text[at:]
    return text


LANE_EXAMPLE = "                      instrumentAnalysis:\n                        queued: 0\n                        running: false\n"
HEALTH_EXAMPLE = ("                    insights:\n                      analyzer: real\n                      analyzerVersion: 1.0.0\n"
                  "                      renderer:\n                        weasyprint: '70.0'\n                        pango: 1.56.3\n"
                  "                      fonts:\n                        archivo: true\n                        fallback: true\n")


def worker_extensions(text: str) -> str:
    """Step 3 for the worker document."""
    text = add_properties(text, "Health", "        insights:\n          $ref: '#/components/schemas/InsightsHealth'\n", True)
    text = add_properties(text, "LanesHealth", "        insights:\n          $ref: '#/components/schemas/LaneHealth'\n", True)
    text = in_block(text, "LanesHealth", 4, "instrument-analysis 1.9.0).", "instrument-analysis 1.9.0, insights 1.11.0).")
    start, end = block(text, "/health", 2)
    body = text[start:end]
    if body.count(LANE_EXAMPLE) < 1 or body.count("                    contractVersion:") != body.count(LANE_EXAMPLE):
        raise SystemExit("/health examples changed shape")
    insights_lane = "                      insights:\n                        queued: 0\n                        running: false\n"
    body = body.replace(LANE_EXAMPLE, LANE_EXAMPLE + insights_lane)
    body = body.replace("                    contractVersion:", HEALTH_EXAMPLE + "                    contractVersion:")
    return text[:start] + body + text[end:]


def backend_extensions(text: str) -> str:
    """Step 3 for the backend document."""
    text = add_enum(text, "AssetKind", ["handout"])
    text = in_block(text, "AssetKind", 4, "`score` (sheet music, 1.7.0).", "`score` (sheet music, 1.7.0) and `handout` (lesson handout PDF, 1.19.0).")
    text = add_enum(text, "AssetOrigin", ["handout"])
    text = in_block(text, "AssetOrigin", 4, "hidden from the library list unless asked for.",
                    "hidden from the library list unless asked for. `handout` (1.19.0): a lesson handout.")
    asset = ("        handout:\n          oneOf:\n            - $ref: '#/components/schemas/HandoutFacts'\n            - type: 'null'\n"
             "          description: Facts of a `handout` item (1.19.0); absent or null for other kinds and before it is ready.\n")
    text = add_properties(text, "Asset", asset, False)
    recipe = ("        handoutRecipe:\n          oneOf:\n            - $ref: '#/components/schemas/HandoutRecipe'\n            - type: 'null'\n"
              "          description: Recipe of a `handout` item (1.19.0; `recipe` and `scoreRecipe` are null then); absent or null otherwise.\n")
    text = add_properties(text, "Provenance", recipe, False)
    track = ("        quality:\n          oneOf:\n            - $ref: '#/components/schemas/QualityBadge'\n            - type: 'null'\n"
             "          description: Stem quality summary (1.19.0); absent or null when no report was ever made.\n")
    return add_properties(text, "Track", track, False)


def tags(text: str, anchor: str) -> str:
    """Step 4: the two new tags before ``health``."""
    new = ("  - name: insights\n    description: Reference-free stem quality estimates and tips (feature 020).\n"
           "  - name: handouts\n    description: Lesson handouts and practice notes (feature 020).\n")
    return replace_once(text, anchor, new + anchor)


def merge_worker(base: str, delta: str) -> str:
    """worker-api 1.11.0."""
    text = append_delta(worker_extensions(base), delta, WORKER)
    text = tags(text, "  - name: health\n    description: Liveness.\n")
    text = re.sub(rf"^  version: {re.escape(WORKER.previous)}$", f"  version: {WORKER.label}", text, count=1, flags=re.MULTILINE)
    return replace_once(text, "    * Jobs are persisted as JSON files;", WORKER_CHANGELOG + "    * Jobs are persisted as JSON files;")


def merge_backend(base: str, delta: str) -> str:
    """backend-api 1.19.0."""
    text = append_delta(backend_extensions(base), delta, BACKEND)
    text = tags(text, "  - name: health\n")
    text = re.sub(rf"^  version: {re.escape(BACKEND.previous)}$", f"  version: {BACKEND.label}", text, count=1, flags=re.MULTILINE)
    text = re.sub(r"^(  x-stemstudio-contains: \[.*)\]$", lambda m: m.group(1) + f", {BACKEND.label}]", text,
                  count=1, flags=re.MULTILINE)
    return replace_once(text, "  license:\n    name: MIT\n", BACKEND_CHANGELOG + "  license:\n    name: MIT\n")


def grows(old: Any, new: Any) -> bool:
    """True when ``new`` equals ``old`` or only adds mapping keys (recursively)."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key in new and grows(value, new[key]) for key, value in old.items())
    return old == new


def extends(old: Any, new: Any) -> bool:
    """``grows`` where lists may gain items (old items kept in order) and descriptions may change."""
    if isinstance(old, dict) and isinstance(new, dict):
        return all(key == "description" or (key in new and extends(value, new[key])) for key, value in old.items())
    if isinstance(old, list) and isinstance(new, list):
        rest = iter(new)
        return all(any(item == other for other in rest) for item in old)
    return old == new


def lost(older: dict[str, Any], newer: dict[str, Any], extended: frozenset[tuple[str, str]]) -> list[str]:
    """Entries of ``older`` that ``newer`` lost or changed (``info`` aside)."""
    found = [f"tags {t['name']}" for t in older["tags"] if t not in newer["tags"]]
    found += [f"paths {n}" for n, v in older["paths"].items()
              if not (extends if ("paths", n) in extended else grows)(v, newer["paths"].get(n))]
    for section in SECTIONS:
        for name, value in older["components"].get(section, {}).items():
            rule = extends if (section, name) in extended else grows
            if not rule(value, newer["components"].get(section, {}).get(name)):
                found.append(f"{section} {name}")
    return found


def leftovers(document: dict[str, Any], target: Target) -> list[str]:
    """Deferred (G2) entries that reached the merged document."""
    names = {n for section in document["components"].values() for n in section}
    found = sorted(target.deferred & names)
    found += [p for p in document["paths"] if p.startswith(G2_PATHS)]
    return [f"deferred entry merged: {n}" for n in found]


def build(target: Target) -> tuple[str, list[str]]:
    """The merged text and its problems."""
    base, delta = target.base.read_text(encoding="utf-8"), target.delta.read_text(encoding="utf-8")
    merged = (merge_worker if target is WORKER else merge_backend)(base, delta)
    document = yaml.safe_load(merged)
    problems = [f"{target.out.name}: {p}" for p in lost(yaml.safe_load(base), document, target.extended)]
    problems += [f"{target.out.name}: {p}" for p in leftovers(document, target)]
    if document["info"]["version"] != target.label:
        problems.append(f"{target.out.name}: version not set")
    if re.search(r"\$ref: '\.\./", merged):
        problems.append(f"{target.out.name}: external reference left")
    return merged, problems


UI_BASE = SPECS / "016-instrument-views/contracts/ui-test-ids.md"
UI_DELTA = HERE / "ui-test-ids.delta.md"
UI_OUT = HERE / "ui-test-ids.md"
UI_CHANGELOG = (
    "1.19.0 (additive, feature 020; merged from `specs/020-insights-handouts/contracts/ui-test-ids.delta.md` by\n"
    "`merge_full.py`, T003) - stem quality panel and badges with tips and actions; lesson handout menu, dialog and\n"
    "library items; setlist handout; practice notes. All 1.15.0 ids are unchanged. Merge order: 016's 1.15.0 plus the\n"
    "020 delta (017-019 are not built yet; the label stays as allocated; the G2 ids are deferred).\n"
)
UI_INTRO = (
    "## Additions in 1.19.0 (feature 020)\n\n"
    "Insights and handouts (design: `specs/020-insights-handouts/`). Every rule above (R1-R10) applies; the level\n"
    "column names the levels at which an id is rendered. `<channel>` is a ChannelId with `.` replaced by `-`\n"
    "(`drums-kick`), `<family>` a handout family, `<code>` a tip or ingredient code, `<assetId>` as in 1.7.0.\n\n"
    "### Ids\n\n"
)


def delta_part(delta: list[str], start: str, stop: str) -> list[str]:
    """Lines of the delta after the heading ``start`` up to the heading ``stop`` (blank edges trimmed)."""
    begin = delta.index(start) + 1
    part = delta[begin:delta.index(stop)]
    while part and not part[0].strip():
        part = part[1:]
    while part and not part[-1].strip():
        part = part[:-1]
    return part


def merge_ui(base: str, delta: str) -> str:
    """ui-test-ids 1.19.0: title, changelog line before the rules, the delta's ids table and rules appended."""
    lines = delta.split("\n")
    table = delta_part(lines, "## Ids", "## Rules added by 020")
    rules = delta_part(lines, "## Rules added by 020", "## Deferred with G2 (not merged in 1.19.0)")
    text = replace_once(base, "# Contract: UI test ids (v1.15.0)\n", "# Contract: UI test ids (v1.19.0)\n")
    text = replace_once(text, "\n\n## Rules\n", "\n" + UI_CHANGELOG + "\n## Rules\n")
    section = UI_INTRO + "\n".join(table) + "\n\n### Rules added by 020\n\n" + "\n".join(rules) + "\n"
    return text.rstrip("\n") + "\n\n" + section


def build_ui() -> tuple[str, list[str]]:
    """The merged ui-test-ids text and its problems (every base line kept in order)."""
    base = UI_BASE.read_text(encoding="utf-8")
    merged = merge_ui(base, UI_DELTA.read_text(encoding="utf-8"))
    body, rest = base.split("\n")[1:], iter(merged.split("\n")[1:])
    problems = [] if all(any(line == other for other in rest) for line in body) else ["ui-test-ids.md: base lines lost"]
    if "**(G2)**" in merged:
        problems.append("ui-test-ids.md: deferred G2 id merged")
    return merged, problems


def outputs() -> list[tuple[Path, str, list[str], str]]:
    """Every output with its merged text, problems and log line."""
    found: list[tuple[Path, str, list[str], str]] = []
    for target in (WORKER, BACKEND):
        merged, problems = build(target)
        found.append((target.out, merged, problems, f"merged {target.base.name} + {target.delta.name} ({target.label})"))
    merged, problems = build_ui()
    found.append((UI_OUT, merged, problems, "merged ui-test-ids.md + ui-test-ids.delta.md (1.19.0)"))
    return found


def main(argv: list[str]) -> int:
    """Entry point."""
    parser = argparse.ArgumentParser(description=(__doc__ or "").split("\n", 1)[0])
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    problems: list[str] = []
    for out, merged, found, log in outputs():
        problems += found
        if args.check and out.read_text(encoding="utf-8") != merged:
            problems.append(f"{out} is not the merge (run without --check)")
        if not args.check and not found:
            out.write_text(merged, encoding="utf-8", newline="\n")
            print(f"{log} -> {out.name}")
    for problem in problems:
        print(problem)
    print("superset: yes" if not problems else "superset: NO")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
