# ADR-033: Instrument views: analyses, timeline views, tablature and transposed parts (feature 016)

- **Status**: Accepted (owner, 2026-11-19, via the Tipoca project site; proposed by the architect, 2026-11-16).
  Architect Phase 0 notes (2026-11-18): T001 reconciled with the merged 012/014/015
  surface (016 DELTA.md "Phase 0 outcome"); T006 decided D6 = native Verovio TAB (see D6).
- **Context documents**: `specs/016-instrument-views/{spec.md, plan.md, research.md R-08..R-12,
  data-model.md 4-7}`, ADR-016 (lanes, library), ADR-017 (notation/engraving), ADR-015 (banner),
  ADR-031, ADR-032, `docs/MoSCoW.md` (every instrument counts equally)

## Context

Feature 016 delivers one view per instrument family in one feature: chords, tablature, drum
trainer, singing feedback and transposed parts. They share needs: per-track analyses (chords,
drum hits, reference melody), drawing synced with playback, and extensions of the 008 score
pipeline. Features 014 (score-follows-playback, sections) and 015 (input, latency, takes) are
specified in parallel; 016 must stay additive on their contracts.

## Decisions

### D1 One analysis job resource in the worker, three typed resources in the API

Worker: `/v1/instrument-analysis-jobs` with a `kind` discriminator (`chords`, `drum-hits`,
`vocal-pitch`) on a new `instrument-analysis` lane, results inline (largest: 90,000 pitch frames).
The resource name is chosen so it cannot collide with a structure/sections analysis resource of
014. API: `/api/tracks/{id}/chords`, `/drum-hits`, `/vocal-pitch` (GET/POST/DELETE), backed by one
`TrackAnalyses` table and one generic service, so the user-facing API reads naturally while the
code has no duplication (SIG).

### D2 Status, fingerprint and auto rules like 006 lyrics

Statuses `none|queued|running|ready|failed|unavailable|stale`; a SHA-256 fingerprint over the
input stems marks results stale after re-separation; chords and drum hits start automatically
(cheap), the reference melody on demand.

### D3 Engine-owned timeline views on the 006 frame loop

The engine gains a `timeline` module (shared clock = the ADR-015 karaoke clock, cursor with
binary search, resize measurement) and four views on it: chord lane (DOM text), banner chords
(extends the karaoke DOM), hit lanes (canvas), pitch view (canvas). Blazor renders empty hosts
with `@key`; the engine owns their children and writes `data-*` host attributes for tests, as for
karaoke. One `requestAnimationFrame` loop, no layout reads per frame, no timers for drawing
(constitution V).

### D4 Score views reuse 014 score-follows-playback

Drum notation, tablature and transposed parts are ordinary 008 score items; 016 adds no score
viewer. Parts keep the concert part's bars (transposition never changes rhythm or bar count), so
014's bar timeline is valid for them unchanged.

### D5 Tablature and parts are options of the 008 score pipeline

`TranscriptionJobRequest` gains `tablature`, `part` and `chords` (all optional, additive); new
profiles `strings`, `brass`, `woodwinds`. Fingering is a Viterbi over note events in
`notation/tablature.py`; parts are a music21 transposition in `notation/parts.py`; MIDI always
stays at concert pitch; drums MIDI uses the General MIDI map. With no new option set the output
is byte-identical to 008 (regression test).

### D6 Tab engraving with Verovio; bounded fallback

Verovio 6.4.0 engraves the TAB staff from our MusicXML (`TAB` clef, staff tuning, string/fret
technical). If the Phase 0 spike shows incorrect tab rendering, audio-ingest draws the TAB staff
into the Verovio SVG pages itself (lines and fret numbers per measure, measure x positions from
Verovio's notation staff); this is a bounded fallback inside `engraving/`, with no new dependency.

**Phase 0 decision (T006, 2026-11-18): native Verovio, no fallback drawing.** Verovio 6.4.0 engraved
notation + TAB and TAB-only parts with 6, 5 and 4 staff lines and the fret numbers as text; it does not
print `<capo>`. The notator therefore also writes a `<direction>` "Capo <n>" above bar 1 (keeping
`<capo>` in `<staff-details>`); `engraving/tablature.py` only checks line counts and fret texts (T037/T038).

### D7 Equal entry point and family rule

A static family table (data-model 5.3) maps channels to families by detail name; it drives
"Practise as ...", the minus-one mixes, "Drumless" and the fixed 004 `play-along-<stem>` presets
(which now mute whole families, also expanded detail strips). Served by
`GET /api/instruments/catalogue` with tunings, instruments, clefs and tolerances so UI, tests and
the 011 agent share one source.

### D8 Contract versions (roadmap allocation)

backend-api 1.15.0, worker-api 1.9.0, audio-engine 2.6.0, ui-test-ids 1.15.0, help-content next
minor (area `instruments`). Planning writes standalone, additive delta documents; Phase 0 merges
them into the full documents on top of the merged 014/015 versions (backend 1.14.0, worker 1.8.0,
engine 2.5.0, ui-test-ids 1.14.0).

## Consequences

- Five families get views in one wave; their stories are independent at the contract level and
  can be built in parallel by the owner agents.
- The engine grows the most (four views, a worklet, a scorer); its module boundaries are set in
  plan.md so SIG limits hold.
- If 014 or 015 changes the assumed names (score view, input node, latency, takes), Phase 0 T001
  adapts only the 016 delta documents, not 014/015.

## Alternatives rejected

| Alternative | Why not |
|---|---|
| Three worker job resources | Triplicated store/route/service code; the kinds differ only in the runner and result schema |
| Blazor-rendered lanes and pitch view | 60 fps interop violates ADR-005's "no 60 fps interop"; drift against the audio clock |
| A separate tab-only score type | Duplicates the 008 pipeline and library handling |
| Transposing parts in the browser | PDF/MusicXML downloads and the agent need the server-side files |
| Storing singing scores | Not requested; privacy; 014 practice log may add it later |