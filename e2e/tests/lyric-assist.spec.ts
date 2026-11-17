import type { LyricAssistPane } from '../pages/lyric-assist-pane';
import type { LyricAssistApi, LyricSuggestionDto, LyricsSheetDto } from '../support/lyric-assist-api';
import { createFixtureSheet, readAssistExpected, requireModelBackend, sheetText } from '../support/lyric-assist-fixtures';
import { runAssist } from '../support/lyric-assist-flow';
import { present } from '../support/generative';
import { expect, test } from '../support/lyric-assist-test';

// Feature 023 US1-US3 on the fake stack (T037, T044, T053 part 1; quickstart Q1, Q2, Q3): the fake
// assistant answers from e2e/lyric-scripts/ by sheet title (zomeravond-check.json, ...); sheets are
// the Phase 0 fixtures with their exact titles. Every test creates its own sheet. Stack:
// STEMSTUDIO_AGENT_BACKEND=fake, STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT=/scripts/lyric (devops T069);
// `npm run test:lyric-assist`.
const zomer = readAssistExpected().sheets.zomeravond;
const planted = zomer.planted ?? [];
const spelling = planted.filter((fact) => fact.category === 'spelling');
const firstTypo = present(spelling[0], 'a planted spelling fix');
const rhythm = present(zomer.rhythm, 'the rhythm facts');
const unknownName = present(zomer.unknownWords?.[0], 'an unknown name').word;

/** A Zomeravond sheet, opened in the editor with the pane shown and checked once. */
async function checkedZomeravond(api: LyricAssistApi, pane: LyricAssistPane): Promise<LyricsSheetDto> {
  await requireModelBackend(api, 'fake');
  const sheet = await createFixtureSheet(api, 'zomeravond');
  await pane.goto(sheet.id);
  await runAssist(pane, api, sheet.id, 'check');
  return sheet;
}

/** Open suggestions whose anchor text is one of `spans`. */
async function openOn(api: LyricAssistApi, lyricsId: string, spans: readonly string[]): Promise<LyricSuggestionDto[]> {
  return (await api.suggestions(lyricsId, { status: 'open' })).filter((suggestion) => spans.includes(suggestion.anchor.original));
}

test('check: dictionary and model layers find the planted corrections and leave the poetic licences alone', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  const sheet = await checkedZomeravond(lyricAssistApi, assistPane);
  await expect(assistPane.byId('lyric-assist-layers')).toHaveAttribute('data-backend', 'fake');
  await expect(assistPane.byId('lyric-assist-layers')).toContainText('OpenTaal');
  for (const fact of planted) {
    const suggestion = await lyricAssistApi.openSuggestionFor(sheet.id, fact.original);
    await expect(assistPane.entry(suggestion.suggestionId), fact.original).toHaveAttribute('data-category', fact.category);
    await expect(assistPane.entry(suggestion.suggestionId)).toHaveAttribute('data-line', String(fact.line));
  }
  // At balanced the recognised licences are not shown ('k, 't, Gij zijt, Naar huis ik ga).
  expect(await openOn(lyricAssistApi, sheet.id, (zomer.licences ?? []).map((licence) => licence.span))).toEqual([]);
  await expect(assistPane.live).toContainText(/Check finished/u);
});

test('accept one fix changes only its span (revision +1, provenance marker); Undo restores it', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  const sheet = await checkedZomeravond(lyricAssistApi, assistPane);
  const typo = firstTypo;
  const suggestion = await lyricAssistApi.openSuggestionFor(sheet.id, typo.original);
  await assistPane.accept(suggestion.suggestionId);
  const after = await lyricAssistApi.sheet(sheet.id);
  expect(after.revision).toBe(sheet.revision + 1);
  expect(after.text).toBe(sheet.text.replace(typo.original, present(typo.proposal, 'proposal')));
  await expect(assistPane.byId(`lyric-line-provenance-${String(typo.line)}`)).toHaveAttribute('data-provenance', 'accepted-suggestion');

  await assistPane.undo.click();
  await expect.poll(async () => (await lyricAssistApi.sheet(sheet.id)).text).toBe(sheet.text);
  await expect(assistPane.entry(suggestion.suggestionId)).toHaveAttribute('data-status', 'open');
});

test('Accept all with the spelling filter is one revision; Undo restores both words', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  const sheet = await checkedZomeravond(lyricAssistApi, assistPane);
  await assistPane.filter('spelling');
  await assistPane.byId('lyric-assist-accept-all').click();
  await expect.poll(async () => (await lyricAssistApi.sheet(sheet.id)).revision).toBe(sheet.revision + 1);
  const accepted = (await lyricAssistApi.sheet(sheet.id)).text;
  for (const fact of spelling) {
    expect(accepted, fact.original).toContain(fact.proposal ?? '');
    expect(accepted, fact.original).not.toContain(fact.original);
  }
  await assistPane.undo.click();
  await expect.poll(async () => (await lyricAssistApi.sheet(sheet.id)).text).toBe(sheet.text);
});

test('strict shows the licences as ideas; an intentional mark is respected until it is removed', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  const sheet = await checkedZomeravond(lyricAssistApi, assistPane);
  await assistPane.setSensitivity('strict');
  await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
  const licences = await openOn(lyricAssistApi, sheet.id, ['Gij zijt', 'Naar huis ik ga']);
  expect(licences.map((licence) => [licence.category, licence.severity])).toEqual([['licence', 'idea'], ['licence', 'idea']]);

  const typo = await lyricAssistApi.openSuggestionFor(sheet.id, firstTypo.original);
  await assistPane.part('intentional', typo.suggestionId).click();
  await expect(assistPane.entry(typo.suggestionId)).toHaveAttribute('data-status', 'intentional');
  await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
  expect(await openOn(lyricAssistApi, sheet.id, [typo.anchor.original])).toEqual([]);

  await assistPane.byId('lyric-marks-open').click();
  const mark = assistPane.byId('lyric-marks-list').locator('[data-testid^="lyric-mark-delete-"]').first();
  await mark.click();
  await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
  expect(await openOn(lyricAssistApi, sheet.id, [typo.anchor.original])).toHaveLength(1);
});

test('"Add to my words" keeps a name out of the check of another Dutch sheet', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  await lyricAssistApi.forgetWord('nl', unknownName);
  await checkedZomeravond(lyricAssistApi, assistPane).then(async (sheet) => {
    const unknown = await lyricAssistApi.openSuggestionFor(sheet.id, unknownName);
    await assistPane.part('add-word', unknown.suggestionId).click();
    await expect(assistPane.entry(unknown.suggestionId)).toHaveCount(0);
  });
  const other = await createFixtureSheet(lyricAssistApi, 'avondlied');
  await assistPane.goto(other.id);
  await runAssist(assistPane, lyricAssistApi, other.id, 'check');
  expect(await openOn(lyricAssistApi, other.id, [unknownName])).toEqual([]);
  await lyricAssistApi.forgetWord('nl', unknownName);
});

test('devices: the rhythm target marks line 2; syllable fit and end rhyme come verified', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  await requireModelBackend(lyricAssistApi, 'fake');
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(sheet.id);
  await assistPane.byId('lyric-rhythm-bpm').fill(String(rhythm.bpm));
  await assistPane.byId('lyric-rhythm-beats').fill(String(rhythm.beatsPerBar));
  await assistPane.byId('lyric-rhythm-bars').fill(String(rhythm.barsPerLine));
  await assistPane.byId('lyric-rhythm-spb').fill(String(rhythm.syllablesPerBeat));
  await assistPane.byId('lyric-rhythm-save').click();
  await assistPane.byId('lyric-syllable-toggle').click();
  await expect(assistPane.byId('lyric-syllables-2')).toHaveAttribute('data-deviates', 'true');
  await expect(assistPane.byId('lyric-syllables-2')).toHaveAttribute('data-target', String(rhythm.targetSyllables));
  await expect(assistPane.byId('lyric-syllables-2')).toContainText(String(rhythm.syllablesLine2));

  await runAssist(assistPane, lyricAssistApi, sheet.id, 'devices');
  const devices = await lyricAssistApi.suggestions(sheet.id, { status: 'open', category: 'device' });
  const fit = present(devices.find((suggestion) => suggestion.type === 'device.syllable-fit'), 'syllable fit');
  const rhyme = present(devices.find((suggestion) => suggestion.type === 'device.end-rhyme'), 'end rhyme');
  expect([fit.anchor.line, fit.syllablesBefore, fit.syllablesAfter]).toEqual([2, rhythm.syllablesLine2, rhythm.targetSyllables]);
  await expect(assistPane.part('syllables', fit.suggestionId)).toContainText(/15\s*(->|→)\s*12/u);
  expect(rhyme.rhymeWith).toEqual({ line: 4, word: 'licht' });
  await expect(assistPane.part('explanation', rhyme.suggestionId)).toContainText('licht');
});

test('writing help: three alternatives for line 4 (accepting one supersedes the rest); Continue inserts nothing before accept', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  await requireModelBackend(lyricAssistApi, 'fake');
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(sheet.id);
  await assistPane.cursorToLine(4);
  await assistPane.byId('lyric-assist-count').selectOption('3');
  await runAssist(assistPane, lyricAssistApi, sheet.id, 'alternatives');
  const alternatives = await lyricAssistApi.suggestions(sheet.id, { status: 'open', category: 'alternative' });
  expect(alternatives).toHaveLength(3);
  await assistPane.accept(alternatives[0]?.suggestionId ?? '');
  for (const other of alternatives.slice(1)) {
    await expect(assistPane.entry(other.suggestionId)).toHaveAttribute('data-status', 'superseded');
  }

  const fresh = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(fresh.id);
  await assistPane.cursorToLine(5);
  await runAssist(assistPane, lyricAssistApi, fresh.id, 'continue');
  const [continuation] = await lyricAssistApi.suggestions(fresh.id, { status: 'open', category: 'continuation' });
  expect(continuation?.anchor.line).toBe(5);
  expect((await lyricAssistApi.sheet(fresh.id)).text).toBe(sheetText('zomeravond'));
});

test('rhyme finder: nacht and night answer within 500 ms without a model call', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  const assistsBefore = (await lyricAssistApi.assists(sheet.id)).length;
  for (const [word, language] of [['nacht', 'nl'], ['night', 'en']] as const) {
    const result = await lyricAssistApi.rhymes(word, language);
    expect(result.rhymes.length, word).toBeGreaterThan(0);
    expect(result.elapsedMs, word).toBeLessThanOrEqual(500);
  }
  await assistPane.goto(sheet.id);
  await assistPane.byId('lyric-rhymes-open').click();
  await assistPane.byId('lyric-rhymes-input').fill('nacht');
  await expect(assistPane.byId('lyric-rhymes-panel').locator('[data-testid^="lyric-rhymes-result-"]').first()).toBeVisible({ timeout: 500 });
  expect(await lyricAssistApi.assists(sheet.id)).toHaveLength(assistsBefore);
});

test('keyboard only (FR-028): Ctrl+Alt+C checks, F8 walks the list, Alt+Enter accepts, Alt+Backspace rejects, Escape returns to the text', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane, page }) => {
  await requireModelBackend(lyricAssistApi, 'fake');
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(sheet.id);
  await assistPane.text.focus();
  await page.keyboard.press('Control+Alt+C');
  await expect.poll(async () => (await lyricAssistApi.assists(sheet.id)).length).toBe(1);
  await lyricAssistApi.waitForEnd((await lyricAssistApi.assists(sheet.id))[0]?.assistId ?? '');
  await expect(assistPane.live).toContainText(/Check finished/u);

  await page.keyboard.press('F8');
  const focused = assistPane.list.locator('[role="option"]:focus, [aria-selected="true"]').first();
  await expect(focused).toBeVisible();
  await expect(assistPane.live).toContainText(/Line \d+/u);
  const firstId = ((await focused.getAttribute('data-testid')) ?? '').replace('lyric-suggestion-', '');
  await page.keyboard.press('Alt+Enter');
  await expect(assistPane.entry(firstId)).toHaveAttribute('data-status', 'accepted');
  await page.keyboard.press('F8');
  const secondId = ((await focused.getAttribute('data-testid')) ?? '').replace('lyric-suggestion-', '');
  await page.keyboard.press('Alt+Backspace');
  await expect(assistPane.entry(secondId)).toHaveAttribute('data-status', 'rejected');
  await page.keyboard.press('Escape');
  await expect(assistPane.text).toBeFocused();
});
