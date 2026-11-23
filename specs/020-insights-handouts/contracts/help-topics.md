# Contract: help topics and feature-catalogue entries for feature 020 (additive)

Follows `specs/004-design-system-ux/contracts/help-content.md` 1.0.0 (format, schema, tests).
Topic files live in `src/StemStudio.Web/Help/topics/<area>/<id>.md` (frontend). Texts are written
by the frontend agent from the facts below and reviewed by the CTO; `reviewed: "020"`.

## Topics

| id | area | title | levels | anchors | Must say |
|---|---|---|---|---|---|
| `stem-quality` | insights | Stem quality | all | `quality-panel`, `quality-badge-*` | what Good/Fair/Poor/Probably not in this song/Not rated mean; that ratings are estimates made without the original recording; when reports are made (after separations, on request) |
| `stem-quality-tips` | insights | Quality tips | all | `quality-tip-*` | each tip in one sentence and what its button does; why there is no "run on the GPU" tip (same models, same result) |
| `stem-quality-measures` | insights | How the estimate works | advanced, expert | `quality-bleed-*`, `quality-artefacts-*`, `quality-index-*` | bleed = parts of a stem that look like a copy of another stem; artefacts = flicker and dropouts the mix does not have; kick and bass playing together can look like bleed |
| `stem-quality-reliability` | insights | How reliable the ratings are | advanced, expert | `quality-analyzer-version` | the validation summary (Phase 0, 2026-11-23: the real-music validation has not run yet, so every rating is a "rough indication"; the thresholds are checked on the synthetic set V1), the analyzer version rule |
| `handouts` | handouts | Lesson handouts | all | `handout-menu`, `setlist-handout-menu`, `asset-handout-*` | one click per family, where the PDF goes, "Make again"; on a setlist: the 014 PDF is the quick print of the set, the setlist handout is the lesson PDF per family kept in the library |
| `handout-families` | handouts | What each handout contains | all | `handout-family-*`, `handout-ingredient-*` | the table of data-model 6.2 in plain words, one paragraph per family; missing parts are left out and listed |
| `handout-transposition` | handouts | Parts for B-flat, E-flat and F instruments | advanced, expert | `handout-transposition` | which instruments use which transposition (trumpet, clarinet, tenor sax: B-flat; alto sax: E-flat; horn: F), chords are transposed too |
| `handout-licence` | handouts | Copyright and handouts | all | `handout-notice` | personal practice and teaching only; the notice is on every page and cannot be removed; transcriptions may contain errors; attributions on the last page; PDFs are not fully accessible because notation pages are pictures |
| `practice-notes` | handouts | Practice notes | all | `practice-notes` | one note per family, printed in that family's handout |
| `reseparate-hq` **(G2, deferred)** | insights | High-quality vocal separation | all | `reseparate-dialog`, `stem-set-pipeline` | what it does, the measured gain from research, time on GPU and CPU, consequences (detail stems removed, mixes kept, library unchanged), model licence status |

Screen topic: none new (the track, setlist and library screen topics get a "See also" link to
`stem-quality` and `handouts`).

## Feature catalogue (`StemStudio.Web/Experience/FeatureCatalog.cs`)

| Feature id | Min level | Help topic |
|---|---|---|
| `quality.report` | Beginner | `stem-quality` |
| `quality.details` | Advanced | `stem-quality-measures` |
| `quality.indices` | Expert | `stem-quality-reliability` |
| `handout.one-click` | Beginner | `handouts` |
| `handout.customise` | Advanced | `handout-families` |
| `handout.transposition` | Advanced | `handout-transposition` |
| `practice-notes.edit` | Beginner | `practice-notes` |
| `reseparation.hq` **(G2, deferred)** | Beginner | `reseparate-hq` |

## Tour (optional, Beginner)

No new tour. The existing Beginner journey rail gains no step; the quality panel shows a one-time
`hint-stem-quality` under the panel (base id pattern `hint-<topicId>`).
