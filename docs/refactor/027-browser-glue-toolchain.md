# 027 Bring the hand-written browser scripts into the TypeScript toolchain and test behaviour, not source text
- Status: proposed · Date: 2026-11-19 · Area: frontend / StemStudio.Web wwwroot/js, web-interop, Web.Tests · Priority: P3

## Problem statement
Next to the two TypeScript packages (`src/audio-engine`, `src/web-interop`: strict type-checked ESLint with SIG limits —
`complexity 5`, `max-params 4`, `max-lines-per-function 30` in `src/audio-engine/eslint.config.js:36-41` — and 90 % coverage gates
in `vitest.config.ts`), four tracked browser scripts (140 lines) live outside any toolchain (`git ls-files 'src/StemStudio.Web/*.js'`):

| File | Lines | Role |
|---|---|---|
| `src/StemStudio.Web/wwwroot/js/focus-trap.js` | 22 | modal focus trap (WCAG 2.4.3, 013 a11y finding) |
| `src/StemStudio.Web/wwwroot/js/dock-inset.js` | 44 | keeps focused controls above the fixed mixer dock (WCAG 2.4.11) |
| `src/StemStudio.Web/wwwroot/js/lyric-editor.js` | 59 | lyric assistant selection and key defaults (023) |
| `src/StemStudio.Web/Components/Recording/InputMeter.razor.js` | 15 | input level for assistive tech (015 FR-021) |

No lint, no type check, no unit test runs on them. Their only tests read the source as text: `RepoPaths.Read*` is used 25 times in
13 test files of `tests/StemStudio.Web.Tests`, for example `Care/BackupDialogTests.cs:279-291` asserts
`script.ShouldContain("addEventListener('focusin'")` and `Mixer/MixerDesignTests.cs:144-154` asserts
`script.ShouldContain("new ResizeObserver(")`; 18 of the 25 reads pin CSS text the same way. Such tests break on a harmless
reformat and pass on a broken script (the strings can stay while the logic is wrong).

The lyric assistant keys are defined three times with no drift test between them: `wwwroot/js/keymap-defaults.json:840-885`
(`externalChords`), `Components/Lyrics/Assist/SuggestionKeys.cs:39-49` and `wwwroot/js/lyric-editor.js:5-13` (`isAssistKey`).
The input-meter range (-60..0 dB) is likewise written both in `InputMeter.razor:8` and `InputMeter.razor.js:10`.

## Impact
- Two of the four scripts carry WCAG 2.2 AA behaviour (focus order, focus not obscured); a regression there is only found by the
  Playwright a11y project, late and in a full stack.
- Source-text tests slow refactoring (every rename of a CSS rule or JS call breaks tests that test no behaviour) and give false
  confidence.
- A key change in one of the three lyric key lists silently desynchronises the help overlay, the .NET handler and the
  browser-default suppression.

## Risk
Risk rating: **low**. Moving the scripts into `src/web-interop` changes only how they are built; the files served under
`wwwroot/js/` keep their names (vite outputs) so `App.razor` and `LyricEditorInterop.ModulePath` stay as they are.
Leaving it: slow a11y regressions, brittle tests. Rollback: restore the hand-written files.

## Effort
S (1–2 days): move four scripts to TypeScript, add vitest/jsdom tests, generate the lyric key predicate from the catalogue,
replace the source-text assertions. Assumes the web-interop build may emit extra entry points (it already emits
`first-paint.js` via `vite build --mode first-paint`).

## Step-by-step plan
1. Safety net: keep the existing Playwright a11y and visual projects as the end-to-end net; note which source-text tests guard
   which behaviour.
2. Port `focus-trap.js`, `dock-inset.js`, `lyric-editor.js` and `InputMeter.razor.js` to `src/web-interop/src/*.ts` as extra vite
   entry points emitting the same file names into `wwwroot/js/` (add them to `wwwroot/js/.gitignore`); lint and type-check them with
   the package rules.
3. Unit-test each in jsdom: focus wraps from the end sentinel to the dialog title and from the start sentinel to the end anchor;
   the inset equals bottom offset + height and is removed when the dock leaves; the meter clamps to -60..0 and stops on dispose;
   the key predicate matches the catalogue.
4. Generate `isAssistKey` from `keymap-defaults.json` `externalChords` (already imported at build time by
   `src/web-interop/src/catalogue.ts:7`), and add a .NET test that `SuggestionKeys.Of` accepts exactly the `externalChords` of scope
   `lyrics-editor` (next to `tests/StemStudio.Web.Tests/Practice/KeyMapDefaultsTests.cs`).
5. Replace the JS source-text assertions with the new unit tests; for CSS, keep only assertions on design-token rules that a
   stylelint rule cannot express, or move them to a stylelint config.
6. Verify: `npm run lint && npm run typecheck && npm run test:coverage` in `src/web-interop` green with the gate; Web tests green;
   Playwright a11y project green. Roll back by reverting.

## Dependencies and related proposals
None blocking. The CSS part relates to `028-breakpoint-tokens.md` (a stylelint rule can enforce the breakpoints).
