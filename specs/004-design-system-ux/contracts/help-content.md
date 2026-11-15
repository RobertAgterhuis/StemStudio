# Contract: help content and tours (v1.0.0, proposed)

Binding for the **frontend** agent (author, loader, renderer, tests) and for every later feature
that adds help (roadmap cross-cutting rule). Schemas: [help-topic.schema.json](help-topic.schema.json),
[tour.schema.json](tour.schema.json) (JSON Schema 2020-12). Rationale: research.md R-06..R-08.

## 1. Files

```text
src/StemStudio.Web/Help/
├── topics/<area>/<id>.md     # one topic per file; file name == id
└── tours/<tourId>.json       # one tour per file; file name == id
```

Both folders are embedded resources of `StemStudio.Web` (`<EmbeddedResource Include="Help/**" />`)
and loaded once at start-up by `HelpCatalog`. A load error (invalid file) fails start-up in
Development and is caught by the unit tests; it can never reach a release build.

## 2. Topic format

```markdown
---
id: mixer-pan
title: Pan
area: mixer
summary: Moves a stem between the left and right speaker. Centre is 0.
feature: mixer.pan            # optional: feature-catalogue id; drives the level note
levels: [advanced, expert]    # optional: who the topic is written for; default all
route: null                   # screen topic only: route template, e.g. "/tracks/{id}/mixer"
anchors: [mixer-pan]          # data-help-id values this topic explains; patterns allowed: "strip-*"
keywords: [stereo, left, right, balance]
related: [mixer-volume, mixer-mute-solo, pan]
reviewed: "004"               # feature that last reviewed the text
---

Pan places a stem in the stereo field ...

See also [Solo](help:mixer-mute-solo).
```

Rules (validated by `help-topic.schema.json` plus the tests in section 4):

| Field | Rule |
|---|---|
| `id` | `^[a-z0-9]+(-[a-z0-9]+)*$`, <= 48 chars, unique, equals the file name |
| `title` | 1-60 chars, sentence case |
| `area` | one of `start`, `import`, `tracks`, `detail`, `mixer`, `mixes`, `glossary` (later features add areas here with a minor version bump) |
| `summary` | 1-140 chars, one or two sentences, plain text (used as tooltip and tour text) |
| `feature` | optional, an id of the feature catalogue (design-spec.md 5) |
| `levels` | optional subset of `beginner`, `advanced`, `expert` |
| `route` | optional; exactly one topic per screen route has it (the screen topic) |
| `anchors` | 0-20 markers; literal ids or a trailing `*` pattern |
| `keywords`, `related` | 0-20 each; `related` entries must be existing topic ids |
| `reviewed` | three-digit feature number |

Body: CommonMark plus pipe tables. Rendered by Markdig with `DisableHtml()` (raw HTML is shown as
text), auto heading ids, no scripts, no images from URLs (images only from
`/_content/help/<file>` if ever needed). Links: `help:<topic-id>` (in-panel navigation),
`route:<path>` (in-app navigation) and `https://` (opens in a new tab with `rel="noopener"`).
Headings start at level 2 (`##`). Length guideline: <= 250 words per topic.

## 3. Tour format

```json
{
  "id": "mixer-basics",
  "version": 1,
  "title": "Mixer basics",
  "route": "/tracks/{id}/mixer",
  "levels": ["beginner"],
  "steps": [
    { "target": "mixer-presets", "topic": "mixer-presets" },
    { "target": "mixer-volume", "topic": "mixer-volume", "text": "Drag a fader or use the arrow keys." }
  ]
}
```

| Field | Rule |
|---|---|
| `id` | kebab-case, unique, equals the file name |
| `version` | integer >= 1; bump when steps or texts change meaningfully (re-offers the tour) |
| `title` | 1-40 chars |
| `route` | route template or `"*"` (any screen) |
| `levels` | non-empty subset of the three levels; the offer is shown only at these levels |
| `steps` | 1-8 steps; `target` = a `data-help-id`; `topic` = a topic id whose title and summary are shown unless `title`/`text` override them (text <= 200 chars) |

Runtime: a step whose target element is missing or not visible is skipped; a tour with no visible
step is not offered.

## 4. Automated checks (FR-026; tests in `tests/StemStudio.Web.Tests/Help/`)

1. Every topic and tour validates against its schema; ids unique; file name == id.
2. Every `related` entry and every `help:` link resolves; every `route:` link matches a page route.
3. Exactly one screen topic per routable page (`@page` directives in `Components/Pages`).
4. Marker coverage: every literal `data-help-id="..."` / `HelpId="..."` in
   `src/StemStudio.Web/**/*.razor` has a topic listing it in `anchors` (patterns allowed), and
   every literal anchor appears at least once in the markup.
5. Every `feature` value exists in the feature catalogue, and every catalogue entry names an
   existing topic.
6. Every tour step `target` is a marker that renders on the tour's route at each of its levels
   (bUnit render with fakes); every `topic` exists.
7. Rendering any topic produces no `<script>`, no raw HTML element from the source and no
   external image.
8. Search: the reference queries (quickstart.md V-H3) return their expected first topic.

## 5. Versioning

This contract is versioned separately from the UI test ids. Adding an area or an optional field
is minor; changing a required field or the file layout is major. Topic and tour versions are
content versions, not contract versions.
