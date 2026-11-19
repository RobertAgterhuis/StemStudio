# 010 One owner for the audio engine across Mixer instances (setlist play, dispose during load)
- Status: proposed · Date: 2026-11-19 · Area: frontend / StemStudio.Web mixer + audio interop · Priority: P2

## Problem statement
The Web Audio engine is a single **circuit-scoped** object that every `Mixer` instance treats as its own, and the Mixer's
async lifecycle has no "disposed" guard. Two concrete failure paths follow from the code:

1. **Setlist play swaps Mixers over one shared engine.**
   - `src/StemStudio.Web/Program.cs:23` registers `AddScoped<IAudioEngine, AudioEngineInterop>()`;
     `src/StemStudio.Web/Recording/RecordingUiRegistration.cs:42` aliases the same instance as `IRecordingJsHost`.
   - `src/StemStudio.Web/Components/Pages/SetlistPlay.razor:19` renders
     `<Mixer @key="entry.Id" ...>`: "next song" destroys the old Mixer and creates a new one in the same circuit.
   - The old Mixer's `DisposeAsync` (`Components/Mixer/Mixer.razor.cs:70-79`) awaits `CloseRecorderAsync()` — which flushes the
     practice log over HTTP when practice was running (`Practice/PracticeRecorder.cs:144-158`, `FlushAsync(afterInFlight: true)`) —
     and `ReleaseKeysAsync()` **before** `Engine.DisposeAsync()`. Blazor does not wait for an async `DisposeAsync` before the new
     component runs its lifecycle.
   - The new Mixer's `OnAfterRenderAsync` (`Mixer.razor.cs:97-112`) calls `Engine.InitializeAsync()`, which returns immediately
     when `_engine` is still set (`Audio/AudioEngineInterop.cs:42-45`), loads its track into the *old* engine, and is then torn
     down when the old Mixer's `Engine.DisposeAsync()` finally runs. Result: the new song's mixer shows "ready" with a dead engine
     (every later call throws `InvalidOperationException("The audio engine is not initialized.")`, `AudioEngineInterop.cs:162-163`,
     which `StartEngineAsync` does not catch — it only catches `AudioEngineException`, `Mixer.razor.cs:127`).
2. **Dispose during the first render.** `OnAfterRenderAsync` awaits `RegisterKeysAsync`, `StartEngineAsync` (stem download and
   decode, seconds) and `StartRecorderAsync` without checking whether the component was disposed meanwhile. When the user leaves
   during `InitializeAsync`, `DisposeAsync` nulls `_engine`/`_module` and disposes `_self` (`AudioEngineInterop.cs:146-155`), and the
   continuing `InitializeAsync` then creates a fresh JS engine (a new `AudioContext`) with a disposed `DotNetObjectReference`:
   a leaked context and failing callbacks. Keys and agent tools are registered after disposal in the same way.
   The same pattern exists in `Components/Recording/InputMeter.razor:21-49`: if disposed while the `import` is pending, `_watch`
   is still null, `DisposeAsync` returns early, and the continuing render starts a 500 ms `setInterval`
   (`InputMeter.razor.js:4`) that is never cleared.
3. **Disposal is not exception-safe.** `Mixer.DisposeAsync` is a straight sequence of awaits; an exception in
   `CloseRecorderAsync` (HTTP) or `ReleaseKeysAsync` skips `Engine.DisposeAsync()` and leaves the `AudioContext` running.
   `AudioEngineInterop.SafeDisposeAsync` (`:187-203`) likewise skips `engine.DisposeAsync()` and the module when the JS `dispose`
   throws.

Evidence of the gap in tests: the Web test run (3 500 tests green, 21 s; coverage 98.7 % lines overall, output in
`.claude/scratchpad/main/webtests/`) has `Components/Pages/SetlistPlay.razor` at **78 %** line coverage, the lowest page; the only
Mixer disposal test is `tests/StemStudio.Web.Tests/Mixer/MixerPageTests.cs:309` (`Dispose_ReleasesEngine_AndUnsubscribes`), which
disposes after a completed start. No test disposes during start or swaps two Mixers over one engine.

## Impact
- Setlist play mode (014 US5) is the live-performance flow: a song that silently has no working engine after "Next" is the
  worst-timed failure in the product. It depends on timing (practice log flush vs. track fetch), so it is intermittent and hard to
  report.
- Leaked `AudioContext`s keep the audio thread and memory alive until the tab closes; browsers cap concurrent contexts.
- Every new mixer feature (recording, score follow, agent tools) adds another await to the same unguarded start/stop sequence.

## Risk
Risk rating: **medium**.
- Leaving it: intermittent dead mixer in setlist play; resource leaks after quick navigation.
- The change touches the engine's ownership model, which every mixer feature uses. Mitigation: keep `IAudioEngine` as the API,
  change only who creates and releases the JS object; characterisation tests first; the JS side already rejects work after
  `dispose()` (`src/audio-engine/src/engine.ts` `alive()`), so the browser contract does not change. Rollback is a revert of the
  Web project only.

## Effort
M (2–4 days): ownership change in `AudioEngineInterop`, guard in `Mixer` and `InputMeter`, exception-safe disposal, bUnit tests
for the three paths and one Playwright check in setlist play. Assumes no change to the audio-engine JS contract.

## Step-by-step plan
1. Safety net (bUnit, `tests/StemStudio.Web.Tests/Mixer/`): (a) dispose while `IAudioEngine.InitializeAsync` / `LoadTrackAsync`
   is pending (a `TaskCompletionSource` stub) and assert no `Register*`/`Attach*` call happens afterwards and the engine is
   disposed exactly once; (b) render `SetlistPlay` with two entries, block the first Mixer's recorder flush, go to the next entry,
   and assert the second Mixer ends with a live engine; (c) a recorder that throws on close still disposes the engine. Mark (a)–(c)
   as expected-to-fail until the fix lands, or land them with the fix in one PR.
2. Ownership: make each Mixer own its engine *session*. Options, pick one in review: (i) register `IAudioEngine` as transient /
   create it through a small `IAudioEngineFactory` so every Mixer gets its own JS engine and `DotNetObjectReference`
   (`IRecordingJsHost` then comes from the Mixer's session, not from DI); or (ii) keep the scoped wrapper but give
   `InitializeAsync` a lease (`await using var lease = await Engine.AcquireAsync()`), where a new lease waits for the previous
   holder's release. Option (i) is simpler and matches "a new mixer per song" in `SetlistPlay.razor:2`.
3. Guard the lifecycle: a `CancellationTokenSource _lifetime` in `Mixer`, cancelled first in `DisposeAsync`; check
   `_lifetime.IsCancellationRequested` after each await in `OnAfterRenderAsync` and release whatever was created after disposal.
   Apply the same pattern to `InputMeter.razor` (dispose the watch when it arrives after disposal).
4. Make `AudioEngineInterop.InitializeAsync` single-flight (cache the `Task`, as `WebInterop.ModuleAsync` does at
   `Interop/WebInterop.cs:86-87`) and make it refuse to create an engine after `DisposeAsync`.
5. Exception-safe disposal: in `Mixer.DisposeAsync`, run each release step in its own `try` and always dispose the engine last
   (`try/finally`); in `SafeDisposeAsync`, dispose the object reference and module in `finally`.
6. Playwright (`e2e/tests/`, tag `@practice-flow` or the setlist spec): practise with logging on for a few seconds, press Next,
   assert the next song plays and the meters move.
7. Verify: the new tests are green; `SetlistPlay.razor` coverage rises above 90 %; manual check in Chromium and Firefox that
   repeated Next/Previous leaves one `AudioContext` (DevTools > Media/WebAudio). Roll back by reverting the PR.

## Dependencies and related proposals
Related: `026-interop-lifetime-helper.md` (shared dispose helper that step 5 can reuse) and
`019-mixer-component-split.md` (a split Mixer makes the lifecycle easier to own, but this fix must not wait for it).
