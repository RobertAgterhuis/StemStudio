"""Merges the 015 recording delta into a full backend contract (architect, 015 Phase 0 T002).

Usage (repo root):
``python -I specs/015-play-along-recording/contracts/merge_recording_delta.py [--check] [base] [out]``
Defaults: base = specs/014-practice-flow/contracts/backend-api.openapi.yaml (014's 1.13.0 = 023's 1.23.0 +
014, the newest full document of the 014+015 round; 015 Phase 0 used 013's 1.12.0), out =
specs/015-play-along-recording/contracts/backend-api.openapi.yaml. The script is text-based so the base
document keeps its formatting, and it fails loudly when an anchor is missing or not unique. ``--check``
rebuilds in memory and fails (exit 1) when ``out`` differs from the merge (freshness).

What it does (DELTA.md 1-2): the delta's paths, its new components (colliding response names are renamed
``Take*``, the shared ``NotFound``/``ProblemDetails``/``ValidationProblemDetails`` keep the base
definitions), the ``*Additions`` merge rules applied to the base schemas and operations, the 013 write gate
on the take operations that delete or replace library files, the ``recording`` tag, and ``info``
(version 1.14.0, ``x-stemstudio-contains`` + 1.14.0, changelog). Requires PyYAML (validation only).
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import yaml

BASE = Path("specs/014-practice-flow/contracts/backend-api.openapi.yaml")
OUT = Path("specs/015-play-along-recording/contracts/backend-api.openapi.yaml")
DELTA = Path("specs/015-play-along-recording/contracts/recording-api.openapi.yaml")
LABEL = "1.14.0"
# Delta responses whose names exist in the base with another meaning: renamed in the merged document.
RENAMED = {
    "BadRequest": "TakeBadRequest",
    "Conflict": "TakeConflict",
    "PayloadTooLarge": "TakePayloadTooLarge",
    "UnsupportedMediaType": "TakeUnsupportedMediaType",
    "Unprocessable": "TakeUnprocessable",
    "WorkerUnavailable": "TakeWorkerUnavailable",
}
# Delta components that the base already defines (kept from the base) or that are merge rules only.
SKIPPED = {"NotFound", "ProblemDetails", "ValidationProblemDetails", "TrackId"}
GATED = {"deleteRecordingTakes", "deleteRecordingTake", "replaceRecordingTakeAudio"}
SECTIONS = ("parameters", "responses", "schemas", "examples")

CHANGELOG = """      1.14.0 (additive, feature 015; ADR-024) - play-along recording, tag `recording`: takes
      (`GET`/`POST /api/tracks/{trackId}/takes`, `POST /api/tracks/{trackId}/takes/delete`,
      `GET`/`PATCH`/`DELETE /api/takes/{takeId}`, `PUT /api/takes/{takeId}/audio`,
      `POST /api/takes/{takeId}/retry`) and input-latency calibrations (`/api/recording/calibrations`);
      `AssetOrigin` `recording` with `Provenance.recording` (`RecordingRecipe`, an optional member like `generation`); `listAssets` query `origin` (without it
      `recording` assets are excluded); `409 managed-by-take` on rename/delete/retry of a take's asset;
      `ExportChannelId` (= `ChannelId` or the literal `take`) for the export channel lists (`ChannelId` itself is unchanged); `CreateAudioExportRequest.takeId`, `ExportMix.take` and `ExportRecipe.takeId` (amended in the round); `MixSettings.take`;
      `Track.takeCount`; deleting a track deletes its takes. New problem types: `invalid-wav`,
      `unsupported-wav-format`, `length-mismatch`, `silent-take`, `take-outside-track`,
      `take-limit-reached`, `recording-speed`, `take-not-ready`, `take-not-in-track`, `take-required`,
      `take-not-failed`, `take-saving`, `capture-missing`, `managed-by-take`. Write gate (1.12.0):
      `deleteRecordingTake`, `deleteRecordingTakes` and `replaceRecordingTakeAudio` are marked
      `x-stemstudio-write-gate: backupRunning`. Merged from `recording-api.openapi.yaml`
      1.14.0-delta.2 by `merge_recording_delta.py`: the delta's `Conflict`, `BadRequest`,
      `PayloadTooLarge`, `UnsupportedMediaType`, `Unprocessable` and `WorkerUnavailable` responses are
      `Take*` here; `NotFound`, `ProblemDetails` and `ValidationProblemDetails` keep their earlier
      definitions. No earlier entry changes except these additions. Merge order: 014's 1.13.0
      (= 023's 1.23.0 + 014) + this delta (re-merged at the 014+015 round start, 015 DELTA.md "Re-merge").
"""


def replace_once(text: str, old: str, new: str) -> str:
    """Replaces a unique anchor; fails when it is missing or ambiguous."""
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"anchor found {count} times: {old[:80]!r}")
    return text.replace(old, new)


def top_block(text: str, key: str) -> tuple[int, int]:
    """Start and end offsets of the body of a top-level key (``paths:``, ``components:``)."""
    match = re.search(rf"^{key}:\n", text, flags=re.MULTILINE)
    if match is None:
        raise SystemExit(f"top-level {key} missing")
    nxt = re.search(r"^\S", text[match.end():], flags=re.MULTILINE)
    return match.end(), match.end() + nxt.start() if nxt else len(text)


def section_end(text: str, section: str) -> int:
    """Offset just after the last line of ``components.<section>`` in a full document."""
    match = re.search(rf"^  {section}:\n", text, flags=re.MULTILINE)
    if match is None:
        raise SystemExit(f"components.{section} missing")
    nxt = re.search(r"^ {0,2}\S", text[match.end():], flags=re.MULTILINE)
    return match.end() + nxt.start() if nxt else len(text)


def delta_components(delta: str) -> dict[str, str]:
    """Raw text of the delta's new components per section (4-space keys), renamed where needed."""
    start, end = top_block(delta, "components")
    body = delta[start:end]
    found: dict[str, str] = {}
    for section in SECTIONS:
        match = re.search(rf"^  {section}:\n", body, flags=re.MULTILINE)
        if match is None:
            continue
        nxt = re.search(r"^  \S", body[match.end():], flags=re.MULTILINE)
        chunk = body[match.end(): match.end() + nxt.start() if nxt else len(body)]
        items = re.split(r"(?=^    \S)", chunk, flags=re.MULTILINE)
        kept = []
        for item in items:
            name = item.split(":", 1)[0].strip()
            if not name or name in SKIPPED or name.endswith("Additions"):
                continue
            if section == "responses" and name in RENAMED:
                item = item.replace(f"    {name}:", f"    {RENAMED[name]}:", 1)
            kept.append(item)
        found[section] = "".join(kept)
    return found


def rename_refs(text: str) -> str:
    """Points the delta's response references at the renamed responses."""
    for old, new in RENAMED.items():
        text = text.replace(f"'#/components/responses/{old}'", f"'#/components/responses/{new}'")
    return text


def gate(paths: str) -> str:
    """Adds the 1.12.0 write gate to the take operations that delete or replace library files."""
    for operation in sorted(GATED):
        paths = replace_once(paths, f"      operationId: {operation}\n", f"      operationId: {operation}\n      x-stemstudio-write-gate: backupRunning\n")
    ops = re.split(r"(?=^    (?:get|post|put|patch|delete):\n|^  /)", paths, flags=re.MULTILINE)
    out = []
    for op in ops:
        if "x-stemstudio-write-gate" in op:
            if "        '409':\n" not in op:
                op = op.rstrip("\n") + "\n        '409':\n          $ref: '#/components/responses/BackupRunning'\n"
            if "        '503':\n" not in op:
                op = op.rstrip("\n") + "\n        '503':\n          $ref: '#/components/responses/Maintenance'\n"
        out.append(op)
    return "".join(out)


def additions(text: str) -> str:
    """Applies the delta's merge rules (DELTA.md 2) to the base schemas and operations."""
    # Phase 0 decision: `ChannelId` stays unchanged (scores and the agent tools use it); only the export channel
    # lists accept the literal `take`, through the new `ExportChannelId`.
    for schema in ("CreateAudioExportRequest", "ExportRecipe"):
        text = in_schema(text, schema, "$ref: '#/components/schemas/ChannelId'", "$ref: '#/components/schemas/ExportChannelId'")
    text = replace_once(
        text,
        "      description: '`export` (1.5.0), `transcription` (1.7.0), `generation` (a take of a generation, 1.8.0).'\n"
        "      enum: [export, transcription, generation]\n",
        "      description: '`export` (1.5.0), `transcription` (1.7.0), `generation` (a take of a generation, 1.8.0). `recording` (1.14.0): a play-along take, hidden from the library list unless asked for.'\n"
        "      enum: [export, transcription, generation, recording]\n",
    )
    # Phase 0 decision: like `generation` (1.8.0), the take's recipe is its own optional member, not a
    # third `recipe` variant (keeps `ExportRecipeDto Recipe` typed for every existing reader).
    text = add_property(text, "Provenance", "recording", [
        "oneOf:", "  - $ref: '#/components/schemas/RecordingRecipe'", "  - type: 'null'",
        "description: The take of a play-along recording (origin `recording`, 1.14.0); absent or null otherwise. `recipe` is null for takes.",
    ])
    text = add_property(text, "Track", "takeCount", [
        "type: integer", "minimum: 0", "maximum: 200",
        "description: Recorded takes of the track (1.14.0); absent = 0. Shown in the delete confirmation.",
    ])
    text = add_property(text, "MixSettings", "take", [
        "oneOf:", "  - $ref: '#/components/schemas/TakeChannelSettings'", "  - type: 'null'",
        "description: The take channel strip (1.14.0); null or absent = none. A saved take id that no longer exists is returned as null.",
    ])
    text = add_property(text, "CreateAudioExportRequest", "takeId", [
        "type: [string, 'null']", "format: uuid",
        "description: \"1.14.0: the take to export. Raw mode with `channels: [\\\"take\\\"]` (default range = the take's span); in mix mode the take of `mix.take` or the saved mix. Errors: 404 unknown take, 409 `take-not-ready`, 422 `take-not-in-track`, 422 `take-required`.\"",
    ])
    # Contract amendment (round 014+015, L4 T071, orchestrator decision): an unsaved mixer state sends its take strip as
    # `ExportMix.take`; the stored recipe names the take (`ExportRecipe.takeId`). Both optional, absent when null.
    text = in_schema(text, "ExportMix", "      properties:\n        masterVolume:\n",
                     "      properties:\n        take:\n          oneOf:\n            - $ref: '#/components/schemas/TakeChannelSettings'\n            - type: 'null'\n"
                     "          description: The take strip of the mixer state (1.14.0); absent or null = no take. Adds channel `take` to the gate evaluation.\n"
                     "        masterVolume:\n")
    text = in_schema(text, "ExportRecipe", "      properties:\n        trackId:\n",
                     "      properties:\n        takeId:\n          type: [string, 'null']\n          format: uuid\n"
                     "          description: The take the export used (1.14.0); absent or null when no take is in it.\n"
                     "        trackId:\n")
    text = replace_once(
        text,
        "      operationId: listAssets\n      summary: List library items, newest first (1.5.0).\n      parameters:\n",
        "      operationId: listAssets\n      summary: List library items, newest first (1.5.0).\n"
        "      description: Without `origin`, assets with origin `recording` (play-along takes, 1.14.0) are excluded; they are managed in the mixer's Takes panel.\n"
        "      parameters:\n"
        "        - name: origin\n          in: query\n          required: false\n"
        "          description: Only items of this origin (1.14.0); `recording` lists play-along takes.\n"
        "          schema:\n            $ref: '#/components/schemas/AssetOrigin'\n",
    )
    return managed_by_take(text)


def add_property(text: str, schema: str, name: str, lines: list[str]) -> str:
    """Adds one property as the first entry of ``properties`` of a top-level schema (``additionalProperties: false``)."""
    pattern = re.compile(rf"(^    {schema}:\n      type: object\n      additionalProperties: false\n(?:      required: \[[^\]]*\]\n)?      properties:\n)", re.MULTILINE)
    matches = pattern.findall(text)
    if len(matches) != 1:
        raise SystemExit(f"schema {schema}: {len(matches)} matches")
    body = "".join(f"          {line}\n" for line in lines)
    return pattern.sub(lambda m: m.group(1) + f"        {name}:\n" + body, text, count=1)


def managed_by_take(text: str) -> str:
    """``409 managed-by-take`` on rename/delete/retry of a take's asset (1.14.0)."""
    note = "1.14.0: `409 managed-by-take` for an asset with origin `recording` (manage it in the Takes panel)."
    text = replace_once(text, "      operationId: renameAsset\n      summary: Rename a library item (names need not be unique).\n",
                        f"      operationId: renameAsset\n      summary: Rename a library item (names need not be unique).\n      description: \"{note}\"\n")
    text = replace_once(text, "        '404':\n          $ref: '#/components/responses/NotFound'\n        '422':\n          $ref: '#/components/responses/UnprocessableRequest'\n    delete:\n      tags: [assets]\n      operationId: deleteAsset\n      summary: Delete a library item and its files (an active render is cancelled).\n",
                        "        '404':\n          $ref: '#/components/responses/NotFound'\n        '409':\n          $ref: '#/components/responses/ManagedByTake'\n        '422':\n          $ref: '#/components/responses/UnprocessableRequest'\n    delete:\n      tags: [assets]\n      operationId: deleteAsset\n      summary: Delete a library item and its files (an active render is cancelled).\n"
                        f"      description: \"{note} The `409` then carries problem type `managed-by-take` instead of `backup-running`.\"\n")
    text = replace_once(text, "            `asset-not-failed` (only failed items can be retried) or `source-unavailable` (the\n            track or one of its stems no longer exists).\n",
                        "            `asset-not-failed` (only failed items can be retried) or `source-unavailable` (the\n            track or one of its stems no longer exists). 1.14.0: `managed-by-take` for an asset with\n            origin `recording` (a failed take is retried with `retryRecordingTake`).\n")
    return text


def in_schema(text: str, schema: str, old: str, new: str) -> str:
    """Replaces a unique string inside one top-level component schema."""
    match = re.search(rf"^    {schema}:\n", text, flags=re.MULTILINE)
    if match is None:
        raise SystemExit(f"schema {schema} missing")
    nxt = re.search(r"^ {0,4}\S", text[match.end():], flags=re.MULTILINE)
    end = match.end() + nxt.start() if nxt else len(text)
    return text[:match.end()] + replace_once(text[match.end():end], old, new) + text[end:]


EXPORT_CHANNEL_ID = """    ExportChannelId:
      type: string
      pattern: '^(take|(vocals|drums|bass|guitar|piano|other)(\\.[a-z][a-z-]{0,31})?)$'
      description: A `ChannelId` or the literal `take` (the take channel, 1.14.0). `take` is valid only when the export names a take (`takeId` in raw mode, `mix.take` or the saved mix's take in mix mode), else 422 `take-required`.
      example: take
"""

MANAGED_BY_TAKE = """    ManagedByTake:
      description: '`managed-by-take`: the asset belongs to a play-along take (origin `recording`); rename, delete and retry it in the Takes panel (1.14.0).'
      content:
        application/problem+json:
          schema:
            $ref: '#/components/schemas/ProblemDetails'
          example:
            type: https://stemstudio.local/problems/managed-by-take
            title: Managed by a take
            status: 409
            detail: This item is a recorded take; manage it in the mixer's Takes panel.
"""


def merge(base: str, delta: str, labels: str) -> str:
    """Returns the merged document text."""
    text = additions(base)
    start, end = top_block(delta, "paths")
    paths = gate(rename_refs(delta[start:end]))
    p_start, p_end = top_block(text, "paths")
    text = text[:p_end] + paths + text[p_end:]
    components = delta_components(rename_refs(delta))
    components["responses"] = components.get("responses", "") + MANAGED_BY_TAKE
    components["schemas"] = components.get("schemas", "") + EXPORT_CHANNEL_ID
    for section in SECTIONS:
        if components.get(section):
            at = section_end(text, section)
            text = text[:at] + components[section] + text[at:]
    text = replace_once(text, "  - name: health\n", "  - name: recording\n    description: Play-along takes and latency calibrations (1.14.0, feature 015).\n  - name: health\n")
    text = re.sub(r"^  version: .*$", f"  version: {LABEL}", text, count=1, flags=re.MULTILINE)
    text = re.sub(r"^(  x-stemstudio-contains: \[.*)\]$", lambda m: m.group(1) + f", {labels}]", text, count=1, flags=re.MULTILINE)
    return replace_once(text, "  license:\n    name: MIT\n", CHANGELOG + "  license:\n    name: MIT\n")


def main(argv: list[str]) -> int:
    """Entry point."""
    check = "--check" in argv[1:]
    args = [arg for arg in argv[1:] if arg != "--check"]
    base = Path(args[0]) if args else BASE
    out = Path(args[1]) if len(args) > 1 else OUT
    merged = merge(base.read_text(encoding="utf-8"), DELTA.read_text(encoding="utf-8"), LABEL)
    document = yaml.safe_load(merged)
    if document["info"]["version"] != LABEL:
        raise SystemExit("version not set")
    if check:
        fresh = out.read_text(encoding="utf-8") == merged
        sys.stdout.write(f"{out}: {'up to date' if fresh else 'NOT the merge (run without --check)'}\n")
        return 0 if fresh else 1
    out.write_text(merged, encoding="utf-8", newline="\n")
    sys.stdout.write(f"merged {base} + {DELTA.name} -> {out} ({len(document['paths'])} paths)\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
