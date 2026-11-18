import type { APIRequestContext, Page } from '@playwright/test';
import type { MixerPage } from '../pages/mixer-page';
import { SectionEditor } from '../pages/section-editor';
import { SectionLane } from '../pages/section-lane';
import type { StemStudioApi } from '../support/api';
import { fakeStems } from '../support/detail';
import { apiBaseUrl, fakeJobTimeoutMs } from '../support/env';
import { PracticeFlowApi, requireDetectOnComplete, sectionsReadyTimeoutMs, type SectionDto, type SectionSetDto } from '../support/practice-flow';
import { expertState } from '../support/preferences';
import { expect, test } from '../support/test';

// Feature 014 US3 (T074; FR-006): editing sections in the mixer (Advanced; re-detect is Expert). Every test starts from a
// freshly detected set of the 20 s music fixture (detect with overwrite), so the edits of one test never reach another
// spec. Boundaries snap like the lane: downbeats on this fixture's reliable grid.
const fixture = 'music_100bpm_20s.mp3';

let context: APIRequestContext;
let flow: PracticeFlowApi;

test.use({ storageState: expertState });

test.beforeEach(async ({ playwright }) => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  requireDetectOnComplete(true);
  context = await playwright.request.newContext({ baseURL: apiBaseUrl });
  flow = new PracticeFlowApi(context);
});

test.afterEach(async () => {
  await context.dispose();
});

interface Editing {
  readonly trackId: string;
  readonly set: SectionSetDto;
  readonly first: SectionDto;
  readonly second: SectionDto;
  readonly lane: SectionLane;
  readonly editor: SectionEditor;
}

/** Re-detects the fixture's sections (fresh, origin detected), opens the mixer and the editor. */
async function startEditing(api: StemStudioApi, mixer: MixerPage, page: Page): Promise<Editing> {
  const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);
  await flow.waitForSections(track.id, fakeJobTimeoutMs);
  expect((await flow.detect(track.id, true)).status()).toBe(202);
  const set = await flow.waitForSections(track.id);
  const [first, second] = set.sections;
  if (first === undefined || second === undefined) {
    throw new Error(`the fake analyser found ${String(set.sections.length)} section(s); the spec needs at least 2`);
  }

  const lane = new SectionLane(page);
  const editor = new SectionEditor(page);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
  await lane.waitForReady(set.sections.length, sectionsReadyTimeoutMs);
  await editor.open();
  await expect(editor.rows).toHaveCount(set.sections.length);
  return { trackId: track.id, set, first, second, lane, editor };
}

async function savedSet(trackId: string, revision: number): Promise<SectionSetDto> {
  await expect.poll(async () => (await flow.sections(trackId)).revision).toBe(revision);
  return flow.sections(trackId);
}

function nearestDistance(points: readonly number[], seconds: number): number {
  return Math.min(...points.map((point) => Math.abs(point - seconds)));
}

/** The second section of a saved set (the spec always has at least two). */
function secondOf(set: SectionSetDto): SectionDto {
  const [, second] = set.sections;
  if (second === undefined) {
    throw new Error('the saved set has fewer than two sections');
  }

  return second;
}

/** A later start for the editor's start field ("Starts at (s)", seconds with 0.1 s steps): 2 s later. */
function laterStart(section: SectionDto): string {
  return (section.startSeconds + 2).toFixed(1);
}

test.describe('section editor (014 US3)', { tag: '@practice-flow' }, () => {
  test('split at the playhead snaps to a downbeat and is saved with Done (scenario 1)', async ({ api, mixer, page }) => {
    const { trackId, set, first, editor } = await startEditing(api, mixer, page);
    const track = await api.getTrack(trackId);
    await mixer.seek(first.startSeconds + ((first.endSeconds - first.startSeconds) / 2) + 0.3);

    await editor.split();
    await expect(editor.rows).toHaveCount(set.sections.length + 1);
    await editor.done();

    const saved = await savedSet(trackId, set.revision + 1);
    expect(saved.origin).toBe('edited');
    expect(saved.sections).toHaveLength(set.sections.length + 1);
    // Section times are kept at 0.1 s (data-model: times rounded to 0.1 s), so the snapped boundary is within 0.05 s.
    expect(nearestDistance(track.beatGrid?.downbeats ?? [], secondOf(saved).startSeconds)).toBeLessThanOrEqual(0.05 + 1e-9);
    expect(saved.sections.map((section) => section.id)).toContain(first.id);
  });

  test('merge keeps the first section kind, label and id (scenario 2)', async ({ api, mixer, page }) => {
    const { trackId, set, first, second, editor } = await startEditing(api, mixer, page);

    await editor.merge(1);
    await expect(editor.rows).toHaveCount(set.sections.length - 1);
    await editor.done();

    const merged = (await savedSet(trackId, set.revision + 1)).sections[0];
    expect(merged).toMatchObject({ id: first.id, kind: first.kind, label: first.label, startSeconds: first.startSeconds, endSeconds: second.endSeconds });
  });

  test('rename and re-kind survive a reload (scenario 4)', async ({ api, mixer, page }) => {
    const { trackId, set, first, lane, editor } = await startEditing(api, mixer, page);

    await editor.rename(1, 'Count-in');
    await editor.setKind(1, 'other');
    await editor.done();
    await savedSet(trackId, set.revision + 1);
    await page.reload();
    await mixer.waitForReady(fakeStems);

    await lane.expectLabel(first.id, 'Count-in');
    await expect(lane.section(first.id)).toHaveAttribute('data-kind', 'other');
    expect((await flow.sections(trackId)).sections[0]?.origin).toBe('user');
  });

  test('moving a boundary keeps both sections at least 1 s long (scenario 3)', async ({ api, mixer, page }) => {
    const { trackId, set, second, editor } = await startEditing(api, mixer, page);
    await editor.setStart(2, laterStart(second));
    await editor.done();

    const moved = await savedSet(trackId, set.revision + 1);
    expect(secondOf(moved).startSeconds).toBeGreaterThan(second.startSeconds);
    for (const section of moved.sections) {
      expect(section.endSeconds - section.startSeconds).toBeGreaterThanOrEqual(1);
    }
  });

});

test.describe('section editor conflicts and re-detection (014 US3)', { tag: '@practice-flow' }, () => {
  test('a save from another window first shows the conflict and keeps the other edit (scenario 5)', async ({ api, mixer, page, context: browser }) => {
    const { trackId, set, first, editor } = await startEditing(api, mixer, page);
    const other = await browser.newPage();
    const otherEditor = new SectionEditor(other);
    await other.goto(`/tracks/${trackId}/mixer`);
    await otherEditor.open();
    await otherEditor.rename(1, 'From the other tab');
    await otherEditor.done();
    await savedSet(trackId, set.revision + 1);

    await editor.rename(1, 'From this tab');
    await editor.done();

    await expect(editor.error).toContainText('changed in another window');
    expect((await flow.sections(trackId)).sections.find((section) => section.id === first.id)?.label).toBe('From the other tab');
    await other.close();
  });

  test('re-detect after edits asks first; cancel keeps the edits, confirm replaces them (scenario 6)', async ({ api, mixer, page }) => {
    const { trackId, set, editor, lane } = await startEditing(api, mixer, page);
    await editor.rename(1, 'Mine');
    await editor.done();
    await savedSet(trackId, set.revision + 1);

    await lane.byId('section-detect-again').click();
    await lane.byId('section-overwrite-cancel').click();
    expect((await flow.sections(trackId)).origin).toBe('edited');
    await lane.byId('section-detect-again').click();
    await lane.byId('section-overwrite-confirm').click();

    // The stored edited set stays `ready` until the new detection replaces it: wait for its revision.
    await expect.poll(async () => (await flow.sections(trackId)).revision, { timeout: sectionsReadyTimeoutMs }).toBe(set.revision + 2);
    const detected = await flow.waitForSections(trackId);
    expect(detected.origin).toBe('detected');
    await lane.waitForReady(detected.sections.length, sectionsReadyTimeoutMs);
  });
});
