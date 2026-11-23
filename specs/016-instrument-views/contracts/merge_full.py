"""Builds the full 016 contracts: worker-api 1.9.0 and backend-api 1.15.0 (architect, 016 Phase 0 T002/T003).

Usage (repo root)::

    python -I specs/016-instrument-views/contracts/merge_full.py [--check]

* worker-api 1.9.0 = 014's full 1.8.0 (``WORKER_BASE``) + ``worker-api.delta.openapi.yaml``.
* backend-api 1.15.0 = 015's full 1.14.0 (``BACKEND_BASE``) + ``backend-api.delta.openapi.yaml``.

The merge is text-based so the base documents keep their formatting (as 015's
``merge_recording_delta.py``) and fails loudly when an anchor is missing or not unique:

1. The delta's paths are appended to ``paths``; references into earlier full documents
   (``../../0xx-.../contracts/<doc>.openapi.yaml#/...``) become internal (``#/...``).
2. The delta's components are appended to the matching ``components`` sections, except the merge
   instructions (``*Extension``) and the components the base already defines (``SHARED``).
3. The ``*Extension`` schemas are applied to their base schemas: properties are inserted as the first
   entries of ``properties`` (worker: also added to ``required``, the worker always sends them;
   backend: optional, absent = null/false/none, DELTA.md "Phase 0 outcome"), enum values are appended.
4. Worker ``Health``/``LanesHealth`` gain ``instrumentAnalysis`` (and the ``/health`` examples with it);
   backend ``listAssets`` gains the ``channel`` query, ``ScoreConflict``/``ScoreUnprocessable`` their codes.
5. Tag ``instrument-views``/``instrument-analysis`` before ``health``; ``info.version``, backend
   ``x-stemstudio-contains`` + 1.15.0, a changelog paragraph.

``--check`` rebuilds both in memory and fails when an output differs (freshness) or when an output is not a
superset of its base (every base entry kept; only ``EXTENDED`` entries may grow). Requires PyYAML.
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
REF = re.compile(r"'\.\./\.\./0\d\d-[a-z0-9-]+/contracts/(?:worker|backend)-api\.openapi\.yaml#")
SECTIONS = ("parameters", "headers", "schemas", "responses", "examples")

WORKER_CHANGELOG = """      1.9.0 (additive, feature 016) - instrument views: a fifth lane `instrument-analysis` (one job
      at a time; never waits for `separate`, `render`, `transcribe` or `practice`) with instrument
      analysis jobs (`/v1/instrument-analysis-jobs/...`, discriminator `kind`): `chords` (BTC or the
      chroma-hmm fallback on the summed harmonic stems, beat-snapped chord chart with the song key and
      key regions), `drum-hits` (the 008 drum onsets per piece) and `vocal-pitch` (pYIN reference
      melody). Results inline; jobs kept 24 h; "interrupted by restart" after a restart. Score
      pipeline (additive): `TranscriptionJobRequest.tablature`, `part`, `chords`, `preferFlats`,
      `singlePiece`; `ScoreProfile` + `strings`, `brass`, `woodwinds`; `ScoreSummary.tablature`,
      `part`, `chordsIncluded`; five warning codes; GM drum MIDI map, MIDI at sounding pitch.
      `Health.instrumentAnalysis`, `Health.lanes.instrumentAnalysis`. Merged from
      `worker-api.delta.openapi.yaml` 1.9.0-delta.2 by `merge_full.py`; the delta's `LibraryPath`
      is the existing component. See ADR-031..033.
"""

BACKEND_CHANGELOG = """      1.15.0 (additive, feature 016; ADR-031..033) - instrument views, tag `instrument-views`: per-track
      analyses chords (`GET`/`POST`/`DELETE /api/tracks/{trackId}/chords`, segment edit, chord sheet,
      ChordPro download, capo suggestions), song key (`GET /api/tracks/{trackId}/key`), drum hits
      (`/api/tracks/{trackId}/drum-hits`), reference melody (`/api/tracks/{trackId}/vocal-pitch`) and
      the static instrument catalogue (`GET /api/instruments/catalogue`); `Track.instrumentViews` and
      `Track.key` (optional: absent = every analysis `none` / no key); `CreateScoreRequest` and
      `ScoreRecipe` + `tablature`, `part`, `includeChords`; `ScoreOptions` + `tablature`, `parts`,
      `chords`; `ScoreFacts` + `tablature`, `part`, `chordsIncluded` (all optional, absent = 008
      behaviour); `ScoreProfile` + `strings`, `brass`, `woodwinds`; five `ScoreWarningCode` values;
      `listAssets` query `channel` (with `trackId`); `409 chords-not-ready` and five `422` codes on
      `createScore`. Merged from `backend-api.delta.openapi.yaml` 1.15.0-delta.2 by `merge_full.py`;
      the delta's `TrackId` parameter is the existing one. No earlier entry changes except these
      additions. Merge order: 015's 1.14.0 + this delta.
"""


@dataclass(frozen=True)
class Target:
    """One full document to build."""

    base: Path
    delta: Path
    out: Path
    label: str
    shared: frozenset[str]
    extended: frozenset[tuple[str, str]]


WORKER = Target(
    SPECS / "014-practice-flow/contracts/worker-api.openapi.yaml",
    HERE / "worker-api.delta.openapi.yaml",
    HERE / "worker-api.openapi.yaml",
    "1.9.0",
    frozenset({"LibraryPath"}),
    frozenset({("schemas", n) for n in ("TranscriptionJobRequest", "ScoreSummary", "ScoreProfile",
                                         "ScoreWarningCode", "Health", "LanesHealth")} | {("paths", "/health")}),
)
BACKEND = Target(
    SPECS / "015-play-along-recording/contracts/backend-api.openapi.yaml",
    HERE / "backend-api.delta.openapi.yaml",
    HERE / "backend-api.openapi.yaml",
    "1.15.0",
    frozenset({"TrackId"}),
    frozenset({("schemas", n) for n in ("Track", "CreateScoreRequest", "ScoreRecipe", "ScoreOptions", "ScoreFacts",
                                         "ScoreProfile", "ScoreWarningCode")}
              | {("responses", "ScoreConflict"), ("responses", "ScoreUnprocessable"), ("paths", "/api/assets")}),
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


def extension_properties(delta: str, name: str) -> str:
    """The ``properties`` children of a delta ``*Extension`` schema, re-indented for the target schema."""
    start, end = block(delta, name, 4)
    p_start, p_end = block(delta[start:end], "properties", 6)
    return delta[start:end][p_start:p_end]


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


def delta_enum(delta: str, name: str) -> list[str]:
    """Values of a delta ``*Extension`` enum."""
    loaded = yaml.safe_load(delta)["components"]["schemas"][name]
    return [str(value) for value in loaded["enum"]]


def append_delta(text: str, delta: str, target: Target) -> str:
    """Steps 1 and 2: paths and components of the delta."""
    d_start, d_end = block(delta, "paths", 0)
    p_start, p_end = block(text, "paths", 0)
    text = text[:p_end] + delta[d_start:d_end] + text[p_end:]
    c_start, c_end = block(delta, "components", 0)
    components = delta[c_start:c_end]
    for section in SECTIONS:
        if f"\n  {section}:\n" not in "\n" + components:
            continue
        s_start, s_end = block(components, section, 2)
        kept = [chunk for name, chunk in items(components[s_start:s_end], 4).items()
                if name not in target.shared and not name.endswith("Extension")]
        at = block(text, "components", 0)[0]
        at = block(text[at:], section, 2)[1] + at
        text = text[:at] + "".join(kept) + text[at:]
    return text


def worker_extensions(text: str, delta: str) -> str:
    """Step 3 and 4 for the worker document."""
    for schema in ("TranscriptionJobRequest", "ScoreSummary"):
        text = add_properties(text, schema, extension_properties(delta, f"{schema}Extension"), schema == "ScoreSummary")
    text = add_enum(text, "ScoreProfile", delta_enum(delta, "ScoreProfileExtension"))
    text = add_enum(text, "ScoreWarningCode", delta_enum(delta, "ScoreWarningCodeExtension"))
    text = add_properties(text, "Health", "        instrumentAnalysis:\n          $ref: '#/components/schemas/InstrumentAnalysisHealth'\n", True)
    text = add_properties(text, "LanesHealth", "        instrumentAnalysis:\n          $ref: '#/components/schemas/LaneHealth'\n", True)
    text = in_block(text, "LanesHealth", 4, "practice 1.8.0).", "practice 1.8.0, instrument-analysis 1.9.0).")
    text = in_block(text, "CompletedTranscriptionJob", 4, "            attribution: null\n",
                    "            attribution: null\n            tablature: null\n            part: null\n            chordsIncluded: false\n")
    start, end = block(text, "/health", 2)
    lane = "                      practice:\n                        queued: 0\n                        running: false\n"
    body = text[start:end].replace(lane, lane + "                      instrumentAnalysis:\n                        queued: 0\n                        running: false\n")
    health = ("                    instrumentAnalysis:\n                      models:\n                        btc-majmin: ready\n"
              "                        btc-large-voca: not-downloaded\n                      chordEngine: btc\n"
              "                      pitchEngine: pyin\n")
    body = body.replace("                    contractVersion:", health + "                    contractVersion:")
    return text[:start] + body + text[end:]


def backend_extensions(text: str, delta: str) -> str:
    """Step 3 and 4 for the backend document."""
    create = extension_properties(delta, "CreateScoreRequestExtension")
    text = add_properties(text, "CreateScoreRequest", create, False)
    text = add_properties(text, "ScoreRecipe", create, False)
    text = add_properties(text, "ScoreOptions", extension_properties(delta, "ScoreOptionsExtension"), False)
    text = add_properties(text, "ScoreFacts", extension_properties(delta, "ScoreFactsExtension"), False)
    text = add_enum(text, "ScoreProfile", delta_enum(delta, "ScoreProfileExtension"))
    text = add_enum(text, "ScoreWarningCode", delta_enum(delta, "ScoreWarningCodeExtension"))
    track = ("        instrumentViews:\n          $ref: '#/components/schemas/InstrumentViewsStatus'\n"
             "        key:\n          oneOf:\n            - $ref: '#/components/schemas/TrackKeySummary'\n            - type: 'null'\n"
             "          description: The song key (1.15.0, S11) when chords are ready; absent or null otherwise.\n")
    text = add_properties(text, "Track", track, False)
    text = in_block(text, "/api/assets", 2, "        - name: status\n",
                    "        - $ref: '#/components/parameters/AssetChannelFilter'\n        - name: status\n")
    text = in_block(text, "ScoreConflict", 4, "        `source-unavailable` (the worker rejected the input; `detail` is the worker's text).\n",
                    "        `source-unavailable` (the worker rejected the input; `detail` is the worker's text).\n"
                    "        1.15.0: `chords-not-ready` (`includeChords` without ready chords).\n")
    return in_block(text, "ScoreUnprocessable", 4, "        was given).\n",
                    "        was given). 1.15.0: `tablature-not-allowed`, `capo-not-allowed`, `tuning-mismatch`,\n"
                    "        `clef-not-allowed`, `part-not-allowed` (data-model.md 4.1-4.3).\n")


def merge_worker(base: str, delta: str) -> str:
    """worker-api 1.9.0."""
    text = append_delta(worker_extensions(base, delta), REF.sub("'#", delta), WORKER)
    text = replace_once(text, "  - name: health\n    description: Liveness.\n",
                        "  - name: instrument-analysis\n    description: Per-track analyses for the instrument views - chords and key, drum hits,"
                        " reference melody (1.9.0, feature 016).\n  - name: health\n    description: Liveness.\n")
    text = re.sub(r"^  version: 1\.8\.0$", f"  version: {WORKER.label}", text, count=1, flags=re.MULTILINE)
    return replace_once(text, "    * Jobs are persisted as JSON files;", WORKER_CHANGELOG + "    * Jobs are persisted as JSON files;")


def merge_backend(base: str, delta: str) -> str:
    """backend-api 1.15.0."""
    text = append_delta(backend_extensions(base, delta), REF.sub("'#", delta), BACKEND)
    text = replace_once(text, "  - name: health\n",
                        "  - name: instrument-views\n    description: Chords and key, capo suggestions, drum hits, reference melody and the"
                        " instrument catalogue (1.15.0, feature 016).\n  - name: health\n")
    text = re.sub(r"^  version: 1\.14\.0$", f"  version: {BACKEND.label}", text, count=1, flags=re.MULTILINE)
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


def build(target: Target) -> tuple[str, list[str]]:
    """The merged text and its problems."""
    base, delta = target.base.read_text(encoding="utf-8"), target.delta.read_text(encoding="utf-8")
    merged = (merge_worker if target is WORKER else merge_backend)(base, delta)
    document = yaml.safe_load(merged)
    problems = [f"{target.out.name}: {p}" for p in lost(yaml.safe_load(base), document, target.extended)]
    if document["info"]["version"] != target.label:
        problems.append(f"{target.out.name}: version not set")
    if re.search(r"\$ref: '\.\./", merged):
        problems.append(f"{target.out.name}: external reference left")
    return merged, problems


def main(argv: list[str]) -> int:
    """Entry point."""
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    problems: list[str] = []
    for target in (WORKER, BACKEND):
        merged, found = build(target)
        problems += found
        if args.check and target.out.read_text(encoding="utf-8") != merged:
            problems.append(f"{target.out} is not the merge (run without --check)")
        if not args.check and not found:
            target.out.write_text(merged, encoding="utf-8", newline="\n")
            print(f"merged {target.base.name} + {target.delta.name} -> {target.out.name} ({target.label})")
    for problem in problems:
        print(problem)
    print("superset: yes" if not problems else "superset: NO")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
