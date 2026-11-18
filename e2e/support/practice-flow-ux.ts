/**
 * Seeded data and screen openers of the feature 014 visual and accessibility suites (T103; FR-031, SC-006): a setlist
 * with a fixed name and content, the sections and the ready score view of the seeded detail track (the 008 score item
 * "UX bass sheet music" of the 004 library screen, so no new item appears on other baselines), and an empty practice log
 * for that track. Everything is found or reset through the API, so the surfaces look the same on every run of a fresh
 * stack; nothing is played, so the practice log stays empty.
 *
 * @packageDocumentation
 */
import { expect, request, type APIRequestContext, type Page } from '@playwright/test';
import { ScorePanel } from '../pages/score-panel';
import { SectionLane } from '../pages/section-lane';
import { SetlistBar, SetlistsPage } from '../pages/setlists-page';
import { setNumericValue } from './controls';
import { apiBaseUrl } from './env';
import { okJson } from './http';
import { PracticeFlowApi, sectionsReadyTimeoutMs, type ScoreViewDto, type SetlistDto } from './practice-flow';
import type { UxData } from './ux-data';
import { ensureScoreItem } from './ux-library';

/** Fixed name of the seeded setlist. */
export const uxSetlistName = 'UX setlist';

/** Note of the seeded setlist and of its first entry. */
export const uxSetlistNotes = { setlist: 'Two songs for the visual suite.', entry: 'Start soft.' } as const;

/** How long the score view of the seeded score item may take to render. */
const scoreViewMs = 60_000;

/** The data the 014 surfaces show. */
export interface PracticeUxData {
  /** The seeded setlist (detail track with the seeded mix and a note, then the plain track). */
  readonly setlist: SetlistDto;
  /** The seeded score item of the detail track and its option text in `score-select`. */
  readonly score: { readonly assetId: string; readonly label: string; readonly view: ScoreViewDto };
  /** Number of sections of the detail track and of the plain track (the click fixture has at least two). */
  readonly sections: { readonly detail: number; readonly plain: number };
}

let seeded: Promise<PracticeUxData> | undefined;

/** Finds or creates the 014 data once per worker (see the file comment). */
export function seedPracticeUx(data: UxData): Promise<PracticeUxData> {
  seeded ??= withApi((context) => seed(context, data));
  return seeded;
}

async function withApi<T>(run: (context: APIRequestContext) => Promise<T>): Promise<T> {
  const context = await request.newContext({ baseURL: apiBaseUrl });
  try {
    return await run(context);
  } finally {
    await context.dispose();
  }
}

async function seed(context: APIRequestContext, data: UxData): Promise<PracticeUxData> {
  const flow = new PracticeFlowApi(context);
  const setlist = await ensureSetlist(context, data);
  const item = await ensureScoreItem(data.detailTrack.id);
  const view = await flow.waitForScoreView(item.id, scoreViewMs);
  const sections = { detail: await ensureSections(flow, data.detailTrack.id), plain: await ensureSections(flow, data.plainTrack.id) };
  await flow.setPracticeLog(true);
  await flow.deletePracticeLog(data.detailTrack.id);
  return { setlist, score: { assetId: item.id, label: `Bass - ${item.name}`, view }, sections };
}

/** Sections of the track, detected now when the stack does not detect them after a separation. */
async function ensureSections(flow: PracticeFlowApi, trackId: string): Promise<number> {
  if ((await flow.sections(trackId)).status === 'none') {
    expect((await flow.detect(trackId)).status()).toBe(202);
  }
  return (await flow.waitForSections(trackId, sectionsReadyTimeoutMs)).sections.length;
}

/** The setlist named {@link uxSetlistName}, (re)set to its fixed note and two entries. */
async function ensureSetlist(context: APIRequestContext, data: UxData): Promise<SetlistDto> {
  const listed = await okJson<readonly { readonly id: string; readonly name: string }[]>(await context.get('/api/setlists'));
  const id =
    listed.find((setlist) => setlist.name === uxSetlistName)?.id ??
    (await okJson<SetlistDto>(await context.post('/api/setlists', { data: { name: uxSetlistName } }))).id;
  await okJson<SetlistDto>(await context.patch(`/api/setlists/${id}`, { data: { note: uxSetlistNotes.setlist } }));
  const entries = [
    { trackId: data.detailTrack.id, mixId: data.mixId, note: uxSetlistNotes.entry },
    { trackId: data.plainTrack.id, mixId: null, note: '' },
  ];
  return okJson<SetlistDto>(await context.put(`/api/setlists/${id}/entries`, { data: { entries } }));
}

/** Opens the mixer of the detail track (or the plain track) with its section lane ready. */
export async function openSectionLane(page: Page, data: UxData, seed: PracticeUxData, track: 'detail' | 'plain' = 'detail'): Promise<SectionLane> {
  const lane = new SectionLane(page);
  const trackId = track === 'detail' ? data.detailTrack.id : data.plainTrack.id;
  await page.goto(`/tracks/${trackId}/mixer`);
  await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
  await lane.waitForReady(seed.sections[track], sectionsReadyTimeoutMs);
  return lane;
}

/**
 * Opens the mixer with the Score panel showing the seeded score, paused inside bar index 1 (the fixed bar of the
 * baselines), and waits until the follower highlights that bar.
 */
export async function openScoreAtBar(page: Page, data: UxData, seed: PracticeUxData): Promise<ScorePanel> {
  await openSectionLane(page, data, seed);
  const panel = new ScorePanel(page);
  await panel.open();
  await panel.choose(seed.score.label, seed.score.assetId, scoreViewMs);
  const image = panel.pageOf(1).locator('img');
  await expect.poll(() => image.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBe(true);
  const bar = seed.score.view.bars[1];
  expect(bar, 'the seeded score has a second bar').toBeDefined();
  await setNumericValue(page.getByTestId('transport-seek'), (bar?.startSeconds ?? 0) + 0.2);
  await expect.poll(() => panel.barIndex()).toBe(1);
  return panel;
}

/** Opens the setlist editor of the seeded setlist with both entries shown. */
export async function openSetlistEditor(page: Page, seed: PracticeUxData): Promise<SetlistsPage> {
  const setlists = new SetlistsPage(page);
  await setlists.open(seed.setlist.id);
  await expect(setlists.entries).toHaveCount(seed.setlist.entries.length);
  return setlists;
}

/** Opens play mode on song 1 of the seeded setlist (the detail track) with its mixer loaded. */
export async function openSetlistPlay(page: Page, data: UxData, seed: PracticeUxData): Promise<SetlistBar> {
  const bar = new SetlistBar(page);
  await page.goto(`/setlists/${seed.setlist.id}/play/1`);
  await bar.expectOn(data.detailTrack.id, 1);
  await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
  return bar;
}

/** Waits for the web fonts (as `openScreen` does before a capture). */
export async function fontsReady(page: Page): Promise<void> {
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
}
