# Hand-off: frontend / polish

## What was built (T112)

- **Keyboard**: space toggles play/pause on the mixer. `IKeyboardShortcuts` / `KeyboardShortcuts` registers a
  document `keydown` listener from `wwwroot/js/mixer-keys.js` (frontend-owned, committed, plain ES module). The
  listener ignores focused inputs, buttons, links and dialogs, so native space behaviour is kept. The
  registration is disposed together with the mixer page.
- **Accessibility**: every `input` and `button` on the mixer has an accessible name. This is asserted by a bUnit
  test over all controls. Faders and pan expose `aria-valuenow/min/max`; pan also has `aria-valuetext`
  ("25 left" / "centre"). Seek has `aria-valuetext` in m:ss.t. Toggles use `aria-pressed`. Strips, transport,
  loop and metronome are labelled groups or toolbars. Errors use `role="alert"` and status texts `role="status"`.
  The name dialog uses `role="dialog"` and `aria-modal`.

## Whole-component summary (all checkpoints)

- Pages: Import `/`, Track detail `/tracks/{id}`, Tracks `/tracks`, Mixer `/tracks/{id}/mixer[?mixId=]`, Mixes
  `/mixes`. Typed API client, audio engine interop (U2 drift test against `types.ts`), Radzen confirm dialog,
  polling timer, and the pure-C# `MixerState`, `LoopValidator`, `MetronomeRules` and `MixNames`.
- **ui-test-ids.md v1.0.0**: every id is rendered. **qa conventions** (CTO message): `aria-pressed` on mute, solo,
  loop and metronome toggles; native range inputs for faders, pan, master, metronome volume and seek; `mix-save`
  on a mix that was never saved opens the name dialog. Every selector in `e2e/pages/*.ts` exists. Transport buttons
  are disabled until the stems are decoded (`waitForReady`).
- **Engine behaviour** (CTO message): `loadTrack` -> `attachMeter` -> full mix; the full mix is re-applied
  after every load and after switching mixes. Disabling the loop sends the last valid region with `enabled:false`,
  and `setLoop` is never sent without a valid region. The position follows only the engine callback. There is
  no metronome meter.

## Red -> green evidence

| Task | Red | Green |
|---|---|---|
| T112 | 3476275 (does not compile: `IKeyboardShortcuts` missing) | 9d267bb |

Earlier pairs: see `frontend-foundational.md`, `frontend-us1.md` .. `frontend-us4.md`.

## Tests and coverage

```
dotnet build StemStudio.slnx --no-incremental        -> 0 Warning(s), 0 Error(s)
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
  Passed! - Failed: 0, Passed: 184, Skipped: 0, Total: 184
  | StemStudio.Web | 99.77% | 94.36% | 100% |   (line | branch | method; gate 90 % line+branch: pass)
dotnet format StemStudio.slnx --verify-no-changes    -> exit 0
```

## Quality checks

- Analyzers (latest-recommended, warnings as errors) are clean. Public types and members have XML docs.
- `tools/quality/sig_check.py` is not in this worktree. By construction, units are 15 LOC or less and take
  4 parameters or fewer. Page logic is split into partial code-behind files (`Mixer.razor.cs`,
  `Mixer.Controls.cs`, `Mixer.Save.cs`) and pure helpers.

## Dependencies added

None (only packages already in `Directory.Packages.props`: Radzen.Blazor, bunit,
Microsoft.Extensions.TimeProvider.Testing, NSubstitute, Shouldly).

## Open issues / requests

- **devops**: `wwwroot/js/mixer-keys.js` is committed source; only `audio-engine.js(.map)` is a build output.
- **qa**: the additive ids are listed in the US1-US4 notes.
- Prerendering is disabled (`InteractiveServerRenderMode(prerender: false)`). The first paint shows "Loading..."
  until the circuit is up.

## Next owner

CTO: review and merge (T119 order: frontend after audio-engine).
