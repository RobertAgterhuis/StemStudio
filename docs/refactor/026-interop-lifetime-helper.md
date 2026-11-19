# 026 One JS-module and handle lifetime helper for all interop wrappers
- Status: proposed · Date: 2026-11-19 · Area: frontend / StemStudio.Web JS interop · Priority: P3

## Problem statement
Seven interop wrappers each re-implement "import a JS module lazily, hand out `IJSObjectReference` handles, call `dispose()` on
them, swallow the errors of a gone circuit", with different rules (`tipoca:reuse`: one concern copied in several places):

| Wrapper | Lazy import | Dispose swallows |
|---|---|---|
| `Interop/WebInterop.cs:86-87, 99-121` (`Registration.SafeAsync`) | cached `Task` (single-flight) | `JSDisconnectedException` only |
| `Interop/VoiceRecorderInterop.cs:56-57, 59-88` (`Recording.SafeAsync`, a copy of the one above) | cached `Task` | `JSDisconnectedException` only |
| `Components/Lyrics/Assist/LyricEditorInterop.cs:66-98` | `_module ??= await ...` (two concurrent callers import twice) | `JSDisconnectedException` only |
| `Audio/AudioEngineInterop.cs:187-203` (`SafeDisposeAsync`) | `_module ??= await ...` | `JSDisconnectedException` or `JSException` |
| `Components/Recording/InputMeter.razor:21-49` | in `OnAfterRenderAsync` | `JSDisconnectedException` only |
| `Components/Mixer/Karaoke/KaraokeBanner.razor.cs:133-143` | via the engine | `InvalidOperationException`, `AudioEngineException`, `JSDisconnectedException` |
| `Preferences/BrowserStorage.cs:45`, `LocalPreferencesStore.cs:21,34` | — | `JSException`, `JSDisconnectedException`, `InvalidOperationException` |

Consequences visible in the code:
- A `JSException` from `dispose()` (handle already released in the browser) propagates out of `WebInterop`/`VoiceRecorderInterop`/
  `LyricEditorInterop` disposal, but is swallowed in `AudioEngineInterop`.
- When the first step throws, the later steps are skipped: `Registration.DisposeAsync` (`WebInterop.cs:115-120`) never disposes
  the `DotNetObjectReference` if `handle.DisposeAsync()` throws something other than `JSDisconnectedException`;
  `RegisterAsync` (`WebInterop.cs:89-95`) leaks the `DotNetObjectReference` when the JS registration call throws.
- `TaskCanceledException` (interop call timeout during teardown) is handled nowhere.
- `KaraokeBanner.razor.cs:141` carries the comment "The mixer released the engine first" — ordering knowledge encoded as an
  exception filter in a child component.

## Impact
Small leaks (`DotNetObjectReference`s pin .NET objects for the circuit's life) and noisy circuit errors on navigation; each
new wrapper (features 010, 015, 023 each added one) copies a slightly different variant, so review effort and the chance of
another inconsistent copy grow.

## Risk
Risk rating: **low**. The helper is additive; wrappers move over one by one with their existing tests
(`tests/StemStudio.Web.Tests/Audio/AudioEngineInteropTests.cs`, `Recording/RecordingInteropTests.cs` and others). Leaving it:
minor leaks and inconsistent error handling. Rollback per wrapper.

## Effort
S (1–2 days): one helper with unit tests, then seven small migrations. Assumes no JS-side change.

## Step-by-step plan
1. Safety net: for each wrapper, a test that disposal completes and releases every reference when the JS `dispose` throws
   `JSException`, `JSDisconnectedException` and `TaskCanceledException` (NSubstitute stubs, as in `AudioEngineInteropTests`).
2. Add `Interop/JsModule.cs`: single-flight lazy import (cached `Task<IJSObjectReference>`), `DisposeAsync` idempotent; and
   `Interop/JsHandle.cs`: wraps a handle plus an optional `DotNetObjectReference`, disposes all three steps in `try/finally`,
   treats `JSDisconnectedException`, `JSException` and `TaskCanceledException` during teardown as "already gone" in one place.
3. Migrate `WebInterop` and `VoiceRecorderInterop` (delete both `SafeAsync` copies), then `LyricEditorInterop`, `InputMeter`,
   `AudioEngineInterop.SafeDisposeAsync`. One PR each or one PR in total; tests green after each.
4. Fix `RegisterAsync` to dispose the receiver when the registration call throws.
5. Verify: the new tests are green; `grep -rn "catch (JSDisconnectedException)" src/StemStudio.Web` finds only the helper.
   Roll back by reverting the migration of the wrapper concerned.

## Dependencies and related proposals
Step 5 of `010-mixer-engine-lifetime.md` (exception-safe engine disposal) can use `JsHandle`; either order works.
