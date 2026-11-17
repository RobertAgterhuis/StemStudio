import type { Page } from '@playwright/test';
import type { AssistButton, LyricAssistPane } from '../pages/lyric-assist-pane';
import type { StemStudioApi } from '../support/api';
import type { LyricAssistApi, LyricsSheetDto } from '../support/lyric-assist-api';
import { corpusTrackTitle, createFixtureSheet, ensureCorpusTrack, readAssistExpected, requireModelBackend, sheetText } from '../support/lyric-assist-fixtures';
import { runAssist } from '../support/lyric-assist-flow';
import type { LyricsApi } from '../support/lyrics-api';
import { expect, test } from '../support/lyric-assist-test';

// Feature 023 guardrails on the fake stack (T053 part 2, T078; quickstart Q3 step 6, Q5; SC-005,
// FR-030..FR-035): imitation instructions are refused, sheets matching library lyrics are refused
// (decision 023-P0-10: the corpus text goes into a track through 006 setLyrics), verbatim model
// output is withheld, injected sheet lines change nothing, and the own-work override (US6) lifts
// only the sheet guard. `npm run test:lyric-assist`.
const expected = readAssistExpected();
const verbatimRun = expected.corpus['t-fake-song'].verbatimRun;
const buttons: readonly AssistButton[] = ['check', 'devices', 'alternatives', 'continue', 'titles'];

/** The not-own sheet ("Lanterns") with the corpus track in place, opened in the editor. */
async function refusedSheet(clients: { readonly api: StemStudioApi; readonly lyricsApi: LyricsApi; readonly lyricAssistApi: LyricAssistApi }, pane: LyricAssistPane): Promise<LyricsSheetDto> {
  await requireModelBackend(clients.lyricAssistApi, 'fake');
  await ensureCorpusTrack(clients.api, clients.lyricsApi);
  const sheet = await createFixtureSheet(clients.lyricAssistApi, 'not-own');
  await pane.goto(sheet.id);
  return sheet;
}

/** Fills the instruction and presses `button`; the start is refused with `imitation` (no assist). */
async function expectImitationRefused(pane: LyricAssistPane, api: LyricAssistApi, sheet: LyricsSheetDto, instruction: string): Promise<void> {
  const before = (await api.assists(sheet.id)).length;
  await pane.byId('lyric-assist-instruction').fill(instruction);
  await pane.press('devices');
  await expect(pane.pane).toContainText(/imitat/iu);
  expect(await api.assists(sheet.id)).toHaveLength(before);
  const problem = await api.startProblem(sheet.id, 'devices', { instruction });
  expect([problem.status, JSON.stringify(problem)]).toEqual([400, expect.stringMatching(/imitation/u)]);
}

/** The page text contains nothing of the withheld verbatim run. */
async function expectNotInPage(page: Page, text: string): Promise<void> {
  expect((await page.content()).toLowerCase()).not.toContain(text.toLowerCase());
}

test('an artist-imitation instruction is refused and starts nothing', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  await requireModelBackend(lyricAssistApi, 'fake');
  const sheet = await createFixtureSheet(lyricAssistApi, 'zomeravond');
  await assistPane.goto(sheet.id);
  await expectImitationRefused(assistPane, lyricAssistApi, sheet, 'zoals Marco Borsato');
  await expect(assistPane.pane).not.toHaveAttribute('data-state', 'running');
});

test('a sheet with library lyrics is refused at every button, naming the track', { tag: '@lyric-assist' }, async ({ api, lyricsApi, lyricAssistApi, assistPane }) => {
  const sheet = await refusedSheet({ api, lyricsApi, lyricAssistApi }, assistPane);
  for (const button of buttons) {
    await assistPane.press(button);
    await expect(assistPane.notOwn, button).toBeVisible();
    await expect(assistPane.notOwn, button).toContainText(corpusTrackTitle);
  }
  const problem = await lyricAssistApi.startProblem(sheet.id, 'check');
  expect([problem.status, problem.code, problem.overridable]).toEqual([409, 'not-own-lyrics', true]);
  expect(await lyricAssistApi.assists(sheet.id)).toEqual([]);
});

test('verbatim model output is withheld and never shown', { tag: '@lyric-assist' }, async ({ api, lyricsApi, lyricAssistApi, assistPane, page }) => {
  await requireModelBackend(lyricAssistApi, 'fake');
  await ensureCorpusTrack(api, lyricsApi);
  const sheet = await createFixtureSheet(lyricAssistApi, 'verbatim');
  await assistPane.goto(sheet.id);
  await assistPane.cursorToLine(2);
  const assist = await runAssist(assistPane, lyricAssistApi, sheet.id, 'alternatives');
  expect(assist.discarded.verbatim).toBe(1);
  await expect(assistPane.withheld).toContainText('1');
  await expect(assistPane.entries).toHaveCount(1);
  await expectNotInPage(page, verbatimRun);
});

test('injected lines in a sheet produce only anchored suggestions and change nothing', { tag: '@lyric-assist' }, async ({ lyricAssistApi, assistPane }) => {
  await requireModelBackend(lyricAssistApi, 'fake');
  const facts = expected.sheets['inject-en'];
  const sheet = await createFixtureSheet(lyricAssistApi, 'inject-en');
  await assistPane.goto(sheet.id);
  const assist = await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');
  const open = await lyricAssistApi.suggestions(sheet.id, { status: 'open' });
  expect(open.map((suggestion) => [suggestion.anchor.original, suggestion.proposal])).toEqual((facts.planted ?? []).map((fact) => [fact.original, fact.proposal]));
  expect(assist.discarded.invalidAnchor + assist.discarded.invalidSchema + assist.discarded.failedVerification + assist.discarded.filtered).toBeGreaterThanOrEqual(3);
  expect((await lyricAssistApi.sheet(sheet.id)).text).toBe(sheetText('inject-en'));
});

test('own work: confirm lifts the sheet guard; verbatim stays withheld, imitation stays refused; revoke refuses again', { tag: '@lyric-assist' }, async ({ api, lyricsApi, lyricAssistApi, assistPane }) => {
  const sheet = await refusedSheet({ api, lyricsApi, lyricAssistApi }, assistPane);
  await assistPane.press('check');
  await expect(assistPane.notOwn).toBeVisible();
  await assistPane.byId('lyric-assist-own-work-open').click();
  await expect(assistPane.byId('lyric-own-work-statement')).not.toBeEmpty();
  await expect(assistPane.byId('lyric-own-work-track')).toContainText(corpusTrackTitle);
  await assistPane.byId('lyric-own-work-cancel').click();
  await assistPane.confirmOwnWork('Robin Tester');
  await expect(assistPane.ownWorkBadge).toContainText('Robin Tester');
  await expect(assistPane.ownWorkBadge).toContainText(corpusTrackTitle);
  expect((await lyricAssistApi.sheet(sheet.id)).assist.ownWorkOverride?.confirmedBy).toBe('Robin Tester');
  await runAssist(assistPane, lyricAssistApi, sheet.id, 'check');

  await assistPane.cursorToLine(2);
  const alternatives = await runAssist(assistPane, lyricAssistApi, sheet.id, 'alternatives');
  expect(alternatives.discarded.verbatim).toBe(1);
  // The sheet itself holds the run (that is why it was refused), so only the suggestions are checked.
  await expect(assistPane.entries.filter({ hasText: verbatimRun })).toHaveCount(0);
  await expectImitationRefused(assistPane, lyricAssistApi, sheet, 'like Ed Sheeran');

  await assistPane.revokeOwnWork();
  expect((await lyricAssistApi.sheet(sheet.id)).assist.ownWorkOverride).toBeNull();
  await assistPane.press('check');
  await expect(assistPane.notOwn).toBeVisible();
});
