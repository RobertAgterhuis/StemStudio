# Hand-off frontend-022: live search keeps every typed key (r22 integration open item 1)

Branch `022-023-round`, 2026-11-17. Fixes the product bug that blocked a green 022 E2E: typing fast into
`library-search` dropped characters (Firefox `library-search.spec.ts:87` and `:157`, Chromium perf spec on 5,000 items).

## Root causes (three, found one after the other)

1. **Own URL echoes taken for navigations.** Blazor Server reports every `?q=` navigation back one round trip later,
   and the router then sets the page's parameters again, so `OnParametersSetAsync` handed the same echo to the search
   a second time after `OnLocationChanged` had consumed it (fix `1ab8337a` matched echoes by URL and consumed them).
   The text was reset to the older echo and rendered into the box. bUnit reproduction:
   `LibrarySearchEchoTests` (late, out-of-order, duplicated, coalesced echoes; echoes between further keys).
2. **Parameters set again before the echo.** A cascading value from the layout re-parameterises the page right after
   the first key, while `Navigation.Uri` is still the old URL: the first search of every E2E test was lost.
3. **The box value was not bound.** `value="@Controller.Text"` with a plain `@oninput` handler: Blazor keeps a rendered
   value in step with what the browser sent (event field info) only for a binder handler, so every render after a key
   wrote the server's text back. A browser probe on the E2E stack (hooked `HTMLInputElement.value`) showed
   `blazor.web.js` setting `q` over `qamwl93kz1` (the box then read `qchf bass`). This was the remaining loss once 1
   and 2 were fixed; it explains the observed symptom (an older value written into the focused box).

## Changes

| Commit | What |
|---|---|
| `cb6f2e02` | Failing bUnit reproduction: `Fakes/DeferredNavigationManager` (navigations come back only when the test releases them, in any order, twice or never) and `Library/LibrarySearchEchoTests`. |
| `9db52012` | `Library/OwnNavigations`: every own navigation carries a history entry state unique to the controller; an own state seen for the first time is an echo, the location taken over last seen again is a repeat, an own state that came back before is Back/Forward, anything else (no state, another instance) is genuine. `LiveSearchController.StartAsync(location, state)` ignores echoes and repeats; `LiveSearchPorts.Navigate` takes the state. A genuine navigation that clears the text also drops a pending debounce. |
| `d33a3815` | `LibraryPage.FollowAsync`: a (URL, history state) pair is taken over once, from `OnParametersSetAsync` or `LocationChanged`. |
| `d75c2be4` | `LibrarySearchBox`: `@bind:get="Controller.Text" @bind:set="OnInput" @bind:event="oninput"`; guard test reads the rendered `oninput` frame (`AttributeEventUpdatesAttributeName == "value"`, `BL0006` suppressed for that one test). |

Design: the box owns the text while the user types; URL sync is one way (text -> `?q=`, replace while typing, push on
Enter); a URL only puts its text in the box on a genuine navigation (load, Back, Forward, a link). WCAG unchanged
(polite live region, labels, focus after clear); test ids unchanged.

## Verification

- `dotnet test tests/StemStudio.Web.Tests`: 2913 passed, 0 failed. Web line 98.68 %, branch 93.85 %;
  `OwnNavigations` 100/100, `LiveSearchController` 100/93.75, `LibrarySearchBox.razor.cs` 100/90.
- `dotnet format StemStudio.slnx --verify-no-changes` clean; e2e `npx tsc --noEmit` and `eslint --max-warnings 0` clean
  (no e2e file changed).
- E2E: see `docs/handoffs/cto-r22-integration.md` (open item 1) and `docs/STATUS.md`.

## Dependencies

None added.

## Open / follow-ups

- The same unbound pattern (`value="@X" @oninput=...`) is in about ten other fields: `AgentPanel` prompt,
  `LyricsEditor`, `LyricsEditorPage`, `LyricsSheetDialog`, `AssistToolbar`, `RhymeFinder`, `SongOptions`,
  `StyleEditor`. They do not write the value back from elsewhere, but a render while keys are in flight can still
  overwrite typed text there; switch them to `@bind:get/@bind:set` in a later round (not in this fix's scope).
- Suggested lesson (frontend): never pair `value="@x"` with a plain `@oninput`; use `@bind` (or `@bind:get/@bind:set`)
  with `@bind:event="oninput"`, so Blazor keeps the field in step with the browser.
