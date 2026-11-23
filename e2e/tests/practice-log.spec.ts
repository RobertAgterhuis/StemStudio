import type { APIRequestContext, Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import { PracticeLogPanel, PracticePage } from '../pages/practice-log-panel';
import { SectionLane } from '../pages/section-lane';
import type { StemStudioApi, TrackDto } from '../support/api';
import { fakeStems } from '../support/detail';
import { apiBaseUrl, apiContextOptions, fakeJobTimeoutMs, webBaseUrl } from '../support/env';
import { positionSeconds } from '../support/levels';
import { PracticeFlowApi, sectionsReadyTimeoutMs } from '../support/practice-flow';
import { expect, test } from '../support/test';

// Feature 014 US6 (T100; FR-025..FR-028, SC-009): the local practice log. The recorder saves at most every 30 s and when
// playback stops or the mixer is left, so each test leaves the mixer before reading the API. Every browser request of the
// spec goes to the app's own origins, the web app and its configured API (stems, artwork), and nowhere else (SC-009,
// ADR-023 D6).
const fixture = 'music_100bpm_20s.flac';

let context: APIRequestContext;
let flow: PracticeFlowApi;
const foreignRequests: string[] = [];

test.beforeEach(async ({ playwright, page }) => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  context = await playwright.request.newContext(apiContextOptions);
  flow = new PracticeFlowApi(context);
  await flow.setPracticeLog(true);
  const own = new Set([new URL(webBaseUrl).origin, new URL(apiBaseUrl).origin]);
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.protocol.startsWith('http') && !own.has(url.origin)) {
      foreignRequests.push(request.url());
    }
  });
});

test.afterEach(async () => {
  await flow.setPracticeLog(true);
  await context.dispose();
});

test.afterAll(() => {
  expect(foreignRequests, 'requests to another host').toEqual([]);
});

async function heard(page: Page): Promise<number> {
  return positionSeconds((await page.getByTestId('transport-position').textContent()) ?? '');
}

/** Plays until the heard position moved by `seconds`, then pauses. */
async function playFor(mixer: MixerPage, page: Page, seconds: number): Promise<void> {
  const start = await heard(page);
  await mixer.play();
  await expect.poll(() => heard(page), { timeout: 30_000 }).toBeGreaterThan(start + seconds);
  await mixer.pause();
}

async function openFresh(api: StemStudioApi, mixer: MixerPage): Promise<TrackDto> {
  const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
  await flow.deletePracticeLog(track.id);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  return track;
}

/** A heard position at least 1 s into the loop and before its middle: the wrap that ended the previous pass is heard. */
function insidePassThree(position: number, section: { readonly startSeconds: number; readonly endSeconds: number } | undefined): boolean {
  const { startSeconds, endSeconds } = section ?? { startSeconds: 0, endSeconds: 0 };
  return position >= startSeconds + 1 && position < (startSeconds + endSeconds) / 2;
}

function firstSectionId(sections: readonly { readonly id: string }[]): string {
  const [first] = sections;
  if (first === undefined) {
    throw new Error('the fake analyser found no section');
  }

  return first.id;
}

test.describe('practice log (014 US6)', { tag: '@practice-flow' }, () => {
  test('playing with pauses records the played time only (scenario 1)', async ({ api, mixer, page }) => {
    const track = await openFresh(api, mixer);
    const log = new PracticeLogPanel(page);
    await log.open();

    await playFor(mixer, page, 3);
    await playFor(mixer, page, 2);

    // Each pause writes the session; wait for the write after the second one (3 s + 2 s heard, so at least 5 s played),
    // not for the first one (flake list).
    await expect.poll(async () => (await flow.practiceSummary(track.id)).todaySeconds, { timeout: 15_000 }).toBeGreaterThanOrEqual(5);
    await page.goto('/');
    const summary = await flow.practiceSummary(track.id);
    expect(summary.sessions).toBeGreaterThanOrEqual(1);
    expect(summary.todaySeconds).toBeLessThan(15);
  });

  test('a section loop with the trainer records loop passes and the tempo reached (scenario 2)', async ({ api, mixer, page, practice }) => {
    const track = await openFresh(api, mixer);
    const set = await flow.waitForSections(track.id, sectionsReadyTimeoutMs);
    const sectionId = firstSectionId(set.sections);
    const lane = new SectionLane(page);
    await lane.waitForReady(set.sections.length, sectionsReadyTimeoutMs);
    await lane.section(sectionId).click();
    await practice.setTrainer({ start: 80, step: 10, target: 100, passes: 1 }, true);

    await mixer.play();
    // data-state, not the text: the waiting text "Ready - starts at 80 % (target 100 %)" already contains "100".
    await expect(practice.trainerStatus).toHaveAttribute('data-state', 'target', { timeout: 90_000 });
    // "target" turns on at the wrap into pass 3; pass 2 (90 %) is reported when that wrap is heard. Pause well inside
    // pass 3, so the pause write holds pass 2 (flake list: the spec pressed Play instead of Pause and read only what the
    // 30 s flush or the leave write of the old circuit had written).
    await expect.poll(async () => insidePassThree(await heard(page), set.sections[0]), { timeout: 15_000 }).toBe(true);
    await mixer.pause();

    const bestTempo = async (): Promise<number> => Math.max(0, ...(await flow.practiceSummary(track.id)).tempo.map((point) => point.percent));
    await expect.poll(bestTempo, { timeout: 15_000 }).toBeGreaterThanOrEqual(90);
    await page.goto('/');
    const summary = await flow.practiceSummary(track.id);
    expect(summary.sections.map((total) => total.sectionId)).toContain(sectionId);
  });

});

test.describe('practice log off and delete (014 US6)', { tag: '@practice-flow' }, () => {
  test('with the log switched off nothing is recorded (scenario 3)', async ({ api, mixer, page }) => {
    const track = await openFresh(api, mixer);
    const practicePage = new PracticePage(page);
    await practicePage.goto();
    await practicePage.enabled.click();
    await expect(practicePage.enabled).toHaveAttribute('aria-checked', 'false');
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    const log = new PracticeLogPanel(page);
    await log.open();
    await expect(log.byId('practice-log-off-note')).toBeVisible();
    await expect(log.byId('practice-log-turn-on')).toBeVisible();

    await playFor(mixer, page, 2);
    await page.goto('/');

    expect((await flow.practiceSummary(track.id)).sessions).toBe(0);
  });

  test('deleting the log of a song brings it back to zero (scenario 4)', async ({ api, mixer, page }) => {
    const track = await openFresh(api, mixer);
    const log = new PracticeLogPanel(page);
    await log.open();
    await playFor(mixer, page, 2);
    await page.goto('/');
    await expect.poll(async () => (await flow.practiceSummary(track.id)).sessions, { timeout: 15_000 }).toBeGreaterThan(0);

    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await log.open();
    await log.deleteTrackLog();

    await expect.poll(async () => (await flow.practiceSummary(track.id)).totalSeconds).toBe(0);
    await expect(log.byId('practice-log-total')).toContainText('0');
  });
});
