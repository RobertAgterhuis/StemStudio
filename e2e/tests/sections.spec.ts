import type { APIRequestContext, Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import { SectionLane } from '../pages/section-lane';
import type { StemStudioApi } from '../support/api';
import { fakeStems } from '../support/detail';
import { apiBaseUrl, fakeJobTimeoutMs } from '../support/env';
import { expectLoop } from '../support/expert-flow';
import { practiceFlowFixturePath } from '../support/fixtures';
import { blurActiveElement } from '../support/help';
import { positionSeconds } from '../support/levels';
import {
  PracticeFlowApi,
  requireDetectOnComplete,
  sectionsReadyTimeoutMs,
  unreliableGridText,
  type SectionDto,
  type SectionSetDto,
} from '../support/practice-flow';
import { expect, test } from '../support/test';

// Feature 014 US1 (T060; FR-001..FR-004, FR-007): song sections in the mixer on the fake stack. The fake structure
// analyser (data-model 2.4) cuts the song into 8-bar sections from the beat grid (intro of 4 bars first), so the
// expected sections come from the API and the UI must show exactly those. Scenario 4 (a track imported before 014)
// needs a stack started with `StemStudio__Practice__SectionDetectOnComplete=false` (`E2E_SECTION_DETECT_ON_COMPLETE=false`).
const clickFixture = 'click_120bpm_4-4_12s.wav';

let context: APIRequestContext;
let flow: PracticeFlowApi;

test.beforeEach(async ({ playwright }) => {
  test.setTimeout(fakeJobTimeoutMs + 90_000);
  context = await playwright.request.newContext({ baseURL: apiBaseUrl });
  flow = new PracticeFlowApi(context);
});

test.afterEach(async () => {
  await context.dispose();
});

/** What a test needs after opening the mixer of the click fixture with its sections. */
interface Opened {
  readonly set: SectionSetDto;
  readonly lane: SectionLane;
  readonly trackId: string;
  readonly first: SectionDto;
  readonly second: SectionDto;
}

async function openWithSections(api: StemStudioApi, mixer: MixerPage, page: Page): Promise<Opened> {
  const track = await api.ensureCompleted(clickFixture, fakeJobTimeoutMs);
  const set = await flow.waitForSections(track.id);
  const [first, second] = set.sections;
  if (first === undefined || second === undefined) {
    throw new Error(`the fake analyser found ${String(set.sections.length)} section(s); the spec needs at least 2`);
  }

  const lane = new SectionLane(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  await lane.waitForReady(set.sections.length, sectionsReadyTimeoutMs);
  return { set, lane, trackId: track.id, first, second };
}

async function transportSeconds(page: Page): Promise<number> {
  return positionSeconds((await page.getByTestId('transport-position').textContent()) ?? '');
}

async function pressOutsideFields(page: Page, key: string): Promise<void> {
  await blurActiveElement(page);
  await page.keyboard.press(key);
}

/** The worker rounds section times to 0.1 s at its output (r14 worker fix, CTO-DECISIONS "Section time precision"). */
const sectionTimeStepSeconds = 0.1;

/** Every section after the first starts on a downbeat of the reliable grid (FR-003), within half a rounding step. */
function expectOnDownbeats(set: SectionSetDto, downbeats: readonly number[]): void {
  expect(set.snap).toBe('downbeat');
  for (const section of set.sections.slice(1)) {
    const nearest = Math.min(...downbeats.map((downbeat) => Math.abs(downbeat - section.startSeconds)));
    expect(nearest, `section ${section.label} starts on a downbeat`).toBeLessThanOrEqual(sectionTimeStepSeconds / 2 + 1e-6);
  }
}

test.describe('section lane after import (014 US1)', { tag: '@practice-flow' }, () => {
  test.beforeEach(() => {
    requireDetectOnComplete(true);
  });

  test('sections appear within 30 s of the separation and match the detected set (scenario 1)', async ({ api, mixer, page }) => {
    const track = await api.ensureCompleted(clickFixture, fakeJobTimeoutMs);
    const { set, lane } = await openWithSections(api, mixer, page);

    await expect(lane.lane).toHaveAttribute('role', 'list');
    await expect(lane.lane).toHaveAttribute('data-revision', String(set.revision));
    for (const section of set.sections) {
      await lane.expectSection(section);
    }
    expectOnDownbeats(set, track.beatGrid?.downbeats ?? []);
  });

  test('a grid the analyser does not trust shows the uncertain-grid note (scenario 5)', async ({ api, mixer, page }) => {
    // A chord pad without drums or onsets has no reliable beat grid (014 T018 fixture): the analyser warns grid-unreliable.
    const track = await api.ensureFileCompleted(practiceFlowFixturePath('practice-song-free.wav'), fakeJobTimeoutMs);
    expect(track.beatGrid?.reliable ?? false, 'the fixture has no reliable beat grid').toBe(false);
    const set = await flow.waitForSections(track.id);
    const lane = new SectionLane(page);

    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await lane.waitForReady(set.sections.length, sectionsReadyTimeoutMs);

    expect(set.snap).not.toBe('downbeat');
    await expect(lane.status).toContainText(unreliableGridText);
  });

  test('detecting again with overwrite refills the lane; Try again exists only after a failure', async ({ api, mixer, page }) => {
    const { lane, trackId, set: before } = await openWithSections(api, mixer, page);
    await expect(lane.retry).toHaveCount(0);

    const again = await flow.detect(trackId, true);

    expect(again.status()).toBe(202);
    await expect.poll(async () => (await flow.sections(trackId)).revision, { timeout: sectionsReadyTimeoutMs }).toBeGreaterThan(before.revision);
    const set = await flow.waitForSections(trackId);
    // The detection was started through the API, not in this page: the lane shows it after a reload.
    await page.reload();
    await mixer.waitForReady(fakeStems);
    await lane.waitForReady(set.sections.length, sectionsReadyTimeoutMs);
    await expect(lane.lane).toHaveAttribute('data-revision', String(set.revision));
  });
});

test.describe('looping and moving between sections (014 US1)', { tag: '@practice-flow' }, () => {
  test.beforeEach(() => {
    requireDetectOnComplete(true);
  });

  test('clicking a section loops it and marks it looped (scenario 2)', async ({ api, mixer, page }) => {
    const { lane, first, second } = await openWithSections(api, mixer, page);

    await lane.section(second.id).click();

    await expectLoop(mixer, second.startSeconds, second.endSeconds);
    await expect(lane.section(second.id)).toHaveAttribute('data-looped', 'true');
    await expect(lane.section(second.id)).toHaveAttribute('aria-pressed', 'true');
    await expect(lane.section(first.id)).toHaveAttribute('data-looped', 'false');
  });

  test('N, Shift+N and Backspace move between sections and back to the current start', async ({ api, mixer, page }) => {
    const { lane, first, second } = await openWithSections(api, mixer, page);
    await mixer.seek(first.startSeconds + 0.5);

    await pressOutsideFields(page, 'n');
    await expect.poll(() => transportSeconds(page)).toBeCloseTo(second.startSeconds, 0);
    await expect.poll(() => lane.currentId()).toBe(second.id);
    await mixer.seek(second.startSeconds + 1.2);
    await pressOutsideFields(page, 'Backspace');
    await expect.poll(() => transportSeconds(page)).toBeCloseTo(second.startSeconds, 0);
    await pressOutsideFields(page, 'Shift+N');
    await expect.poll(() => transportSeconds(page)).toBeCloseTo(first.startSeconds, 0);
    await expect.poll(() => lane.currentId()).toBe(first.id);
  });

  test('the transport-row section buttons do what the keys do', async ({ api, mixer, page }) => {
    const { lane, first, second } = await openWithSections(api, mixer, page);
    await mixer.seek(first.startSeconds + 0.5);

    await lane.byId('section-next').click();
    await expect.poll(() => transportSeconds(page)).toBeCloseTo(second.startSeconds, 0);
    await lane.byId('section-loop-current').click();
    await expectLoop(mixer, second.startSeconds, second.endSeconds);
    await lane.byId('section-previous').click();
    await expect.poll(() => transportSeconds(page)).toBeCloseTo(first.startSeconds, 0);
  });
});

test.describe('tracks without sections (014 US1 scenario 4)', { tag: '@practice-flow' }, () => {
  test.beforeEach(() => {
    requireDetectOnComplete(false);
  });

  test('opening the mixer of a track without sections starts detection and fills the lane', async ({ api, mixer, page }) => {
    const track = await api.ensureCompleted(clickFixture, fakeJobTimeoutMs);
    expect((await flow.sections(track.id)).status).toBe('none');
    const lane = new SectionLane(page);

    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);

    await expect(lane.lane).toHaveAttribute('data-status', /queued|detecting|ready/u, { timeout: 10_000 });
    const set = await flow.waitForSections(track.id);
    await lane.waitForReady(set.sections.length, sectionsReadyTimeoutMs);
  });
});
