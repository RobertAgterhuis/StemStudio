# Questions: frontend / 020

Lane FRONTEND, branch `020-lane-frontend` (from design head ca82a055). Asked 2026-11-23 before any code.

## Q1 (T026/T027): help areas `insights` and `handouts` are not in any help-topic schema

`contracts/help-topics.md` puts the new topics in the areas `insights` and `handouts`. The topic schema the help
tests validate against is `specs/016-instrument-views/contracts/help-topic.schema.json` (help-content 1.1.0), whose
`area` enum is `start, import, tracks, detail, mixer, mixes, glossary, instruments`. help-content.md section 5 says a
new area is a minor version bump of the help-content contract; 020 ships no such schema, and the schema lives in an
architect-owned path. Without it every new topic fails `HelpCatalogTests`/`Feature004SkeletonTests`.

Proposal (A): the architect adds `specs/020-insights-handouts/contracts/help-topic.schema.json` (help-content
**1.2.0**: area enum += `insights`, `handouts`; nothing else changes). Frontend then points the test project's
schema copy at it (as 016 did in its Phase 0 T004) and extends `TopicParser.AreaOrder` with `insights` and
`handouts` after `instruments` (help index titles "Stem quality" and "Lesson handouts").

Alternative (B): keep 1.1.0 and file the topics under the existing areas `tracks` (quality) and `mixes` (handouts).
This deviates from help-topics.md, so only with your approval.

## Q2 (T053/T054): what is "the current mix" for the tip action `mute`?

T053 says "mute patches the current mix"; US2 AS4 says "offers Mute this stem for the current mix"; plan.md flow 3
says "mute in the current mix". The tips are shown on the track page (`quality-tip-action-*`), which has no current
mix: the mixer starts from defaults unless `?mixId=` is given, and a track can have zero or many saved mixes. No
contract names which mix is meant.

Proposal (A): the tip button opens the mixer with the stem muted in the live (unsaved) mixer state:
`/tracks/{id}/mixer?mute=<channel>` (new optional query parameter of the mixer page, frontend-owned; the user saves
as usual). No API call, nothing is changed behind the user's back, works with zero mixes.

Alternative (B): PATCH the track's most recently updated saved mix (`ListMixesAsync(trackId)`, newest `updatedAt`)
with the channel muted (`UpdateMixAsync`) and show a toast "Muted piano in <mix name>"; with no saved mix the button
falls back to (A). This changes a saved mix from the track page.

## Q3 (confirm my reading, no blocker): `open-detail` and ingredient create links

- `open-detail` (`use-detail-stem`, `listen-to-rest`): navigate to `/tracks/{id}/mixer?expand=<parent>` (new optional
  mixer query parameter; the mixer expands that split parent once the engine is ready).
- Ingredient `action` links in the dialog (`handout-ingredient-action-<code>`) map to existing flows only:
  `create-score`/`create-tab`/`create-drum-notation`/`create-transposed-part` open the track page score dialog (008/016)
  for the family's stem; `detect-chords` and `find-lyrics` go to the track page lyrics/chords card; `detect-sections`
  to the mixer; `split-drums` starts nothing, it scrolls to the detail section; `write-notes` focuses
  `practice-notes-text` with the family selected. In the dialog they are links/buttons that close the dialog first.

## Answers (orchestrator, 2026-11-23)

Q1 -> A (one-time exception: `specs/020-insights-handouts/contracts/help-topic.schema.json`, help-content 1.2.0).
Q2 -> A (`/tracks/{id}/mixer?mute=<channel>`, unsaved, no API call). Q3 -> confirmed.

## Q4 (T027): the feature id `practice-notes.edit` breaks the help-content id pattern

`contracts/help-topics.md` names the catalogue entry `practice-notes.edit` (topic `practice-notes`, Beginner). Both
the help-topic schema (`feature`: `^[a-z]+(\.[a-z0-9-]+)+$`, 004 and the new 1.2.0) and the 004 catalogue test allow no
hyphen in the first segment, so the topic's `feature: practice-notes.edit` fails schema validation and the entry fails
`FeatureCatalogTests`. Everything else of T026/T027 is green locally (uncommitted until this is settled).

Proposal (A): use `practice.notes-edit` (valid; sits with the other `practice.*` entries; same level and topic) and
record it as an approved correction of help-topics.md in my hand-off.

Alternative (B): `notes.practice` or another valid id you prefer. Changing the pattern itself is not proposed (it is
a 004 rule that every feature id follows).
