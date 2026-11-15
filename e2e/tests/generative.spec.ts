import { fakeStems } from '../support/detail';
import { requireEnv } from '../support/feature-skips';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { expectAiLabels, generationTimeoutMs, restyledTake, slowGenerationTimeoutMs, sourceTrack, takeOf } from '../support/generative-flow';
import { present, readFakeGenerator, requireGeneratorMode, uniqueName } from '../support/generative';
import { expectProblem, terminalStates } from '../support/generation-api';
import { restartService } from '../support/generator-stack';
import { expectHelpMarker } from '../support/help';
import { beginnerHiddenRestyleIds, everyLevelRestyleIds, expectNotRendered, expectRendered } from '../support/levels';
import { expectPlayable } from '../support/media';
import { levelState } from '../support/preferences';
import { expect, test } from '../support/generative-test';

// Feature 009 US1 (restyle), US3 (own style in use), US4 (import a take), US5 (in place) and the
// fault modes (009:T045, T062, T067; FR-001, FR-002, FR-006-FR-009, FR-012, FR-014; SC-005,
// SC-006, SC-007). Fake generator stack (quickstart Q2); each case runs only in the
// STEMSTUDIO_FAKE_GENERATOR_MODE it asserts.
const fake = readFakeGenerator();

test.beforeEach(async ({ generationApi }) => {
  test.setTimeout(fakeJobTimeoutMs + 3 * generationTimeoutMs);
  await generationApi.requireState('ready');
});

test.describe('restyle a track', { tag: '@generative' }, () => {
  test.beforeEach(() => {
    requireGeneratorMode('ready', 'oom');
  });

  test('"Jazz trio" in four interactions: progress, a playable AI-labelled take with details (US1-1, US1-4, US2-3, SC-007)', async ({ api, generationApi, generations, restyle, trackPage }) => {
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    const before = (await generationApi.list({ sourceTrackId: track.id })).map((g) => g.id);
    await trackPage.gotoLoaded(track.id);
    await restyle.open();
    await restyle.chooseStyle(jazz.name);
    await expect(restyle.description).toContainText(present(jazz.description, 'preset description'));
    await expect(restyle.strength).toHaveAttribute('aria-valuetext', new RegExp(jazz.defaultStrength, 'iu'));
    await expect(restyle.notice).toContainText(/personal use/iu);
    await restyle.start();
    const generation = await generationApi.waitForNewest({ sourceTrackId: track.id }, before);
    await expect(generations.part('progress', generation.id)).toHaveAttribute('aria-valuenow', /^\d+$/u);
    await generations.waitForStatus(generation.id, 'completed', generationTimeoutMs);
    await expectPlayable(generations.take('play', generation.id));
    await expect(generations.take('ai-badge', generation.id)).toContainText('AI');
    const done = await generationApi.get(generation.id);
    const take = takeOf(done);
    const range = present(done.restyle, 'restyle details').range;
    expect(take.durationSeconds).toBeCloseTo(range.endSeconds - range.startSeconds, 0);
    const details = await generations.openDetails(generation.id);
    for (const text of [jazz.name, String(take.seed), track.title, take.model.id]) await expect(details).toContainText(text);
    await expectAiLabels(generationApi, take);
  });

  test('range, stems, strength and two takes are applied (FR-002, FR-006)', async ({ api, generationApi, restyle, trackPage }) => {
    const track = await sourceTrack(api);
    const before = (await generationApi.list({ sourceTrackId: track.id })).map((g) => g.id);
    await trackPage.gotoLoaded(track.id);
    await restyle.open();
    await restyle.fill({ style: 'Synthwave', range: { start: 2, end: 14 }, excludeStems: ['vocals'], strength: 1, takes: 2 });
    await restyle.start();
    const queued = await generationApi.waitForNewest({ sourceTrackId: track.id }, before);
    const done = await generationApi.waitForCompleted(queued.id, generationTimeoutMs);
    const details = present(done.restyle, 'restyle details');
    expect([details.range.startSeconds, details.range.endSeconds, details.range.mode, details.strength]).toEqual([2, 14, 'range', 'light']);
    expect(details.includedStems).not.toContain('vocals');
    expect(done.takes.map((t) => Math.round(t.durationSeconds))).toEqual([12, 12]);
    expect(new Set(done.takes.map((t) => t.seed)).size).toBe(2);
    await expectAiLabels(generationApi, takeOf(done, 2));
  });

  test('in place: the take keeps the length of the track (US5-1, FR-014)', async ({ api, generationApi, restyle, trackPage }) => {
    const track = await sourceTrack(api);
    const before = (await generationApi.list({ sourceTrackId: track.id })).map((g) => g.id);
    await trackPage.gotoLoaded(track.id);
    await restyle.open();
    await restyle.fill({ style: 'Jazz trio', mode: 'inPlace', range: { start: 5, end: 15 } });
    await restyle.start();
    const done = await generationApi.waitForCompleted((await generationApi.waitForNewest({ sourceTrackId: track.id }, before)).id, generationTimeoutMs);
    expect(present(done.restyle, 'restyle details').range.mode).toBe('inPlace');
    expect(takeOf(done).durationSeconds).toBeCloseTo(present(track.durationSeconds, 'track duration'), 0);
  });

});

test.describe('restyle: limits and own styles', { tag: '@generative' }, () => {
  test.beforeEach(() => {
    requireGeneratorMode('ready', 'oom');
  });

  test('the API refuses ranges and take counts outside the limits (edge cases, FR-002)', async ({ api, generationApi }) => {
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    const tooShort = await expectProblem(await generationApi.restyle(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 5 }), 400);
    expect(Object.keys(tooShort.errors ?? {}).join(' ')).toMatch(/range/iu);
    await expectProblem(await generationApi.restyle(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 5, rangeEndSeconds: 40 }), 400);
    await expectProblem(await generationApi.restyle(track.id, { styleId: jazz.id, mode: 'range', takes: 3 }), 400);
  });

  test('an own style restyles and the take keeps its snapshot after the style changes (US3, US3-3, FR-006)', async ({ api, generationApi, generations, trackPage }, info) => {
    const track = await sourceTrack(api);
    const copy = await generationApi.duplicateStyle((await generationApi.preset('Bossa nova')).id);
    const name = uniqueName('My bossa', info.project.name, runId());
    expect((await generationApi.updateStyle(copy.id, { ...copy, name, moods: ['dreamy', 'warm'] })).status()).toBe(200);
    const queued = await generationApi.restyleOk(track.id, { styleId: copy.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 12 });
    await generationApi.waitForCompleted(queued.id, generationTimeoutMs);
    expect(await generationApi.deleteStyle(copy.id)).toBe(204);
    const done = await generationApi.get(queued.id);
    expect([done.restyle?.style.name, done.restyle?.style.kind]).toEqual([name, 'user']);
    await trackPage.gotoLoaded(track.id);
    await expect(await generations.openDetails(queued.id)).toContainText(name);
  });
});

test.describe('restyle at Beginner', { tag: '@generative' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('the dialog shows style, notice and start; the options are not rendered (ui-test-ids 1.8.0, R7)', async ({ api, page, restyle, trackPage }) => {
    requireGeneratorMode('ready', 'oom');
    await trackPage.gotoLoaded((await sourceTrack(api)).id);
    await restyle.open();
    await expectRendered(page, everyLevelRestyleIds);
    await expectNotRendered(page, beginnerHiddenRestyleIds(fakeStems));
    await expect(restyle.dialog).toBeVisible();
    await expectHelpMarker(restyle.dialog, 'restyle-options');
    await expectHelpMarker(restyle.dialog, 'generation-personal-use');
  });
});

test.describe('import a take', { tag: '@generative' }, () => {
  test('the take becomes a separated, mixable AI-labelled track linked to its source; again returns it (US4-1, US4-2, FR-009)', async ({ api, generationApi, generations, mixer, page, trackList, trackPage }) => {
    requireGeneratorMode('ready', 'oom');
    const source = await sourceTrack(api);
    const done = await restyledTake(generationApi, source.id, 'Jazz trio');
    await trackPage.gotoLoaded(source.id);
    await generations.take('import', done.id).click();
    await expect.poll(async () => takeOf(await generationApi.get(done.id)).importedTrackId ?? '').not.toBe('');
    const trackId = present(takeOf(await generationApi.get(done.id)).importedTrackId, 'imported track');
    expect((await api.waitForTerminal(trackId, fakeJobTimeoutMs)).status).toBe('completed');
    await mixer.goto(trackId);
    await expect(page.getByTestId('strip-vocals')).toBeVisible();
    await trackPage.gotoLoaded(trackId);
    await expect(trackPage.originBadge).toContainText(/AI-generated/iu);
    await expect(trackPage.originSourceLink).toHaveAttribute('href', new RegExp(source.id, 'u'));
    await page.goto('/tracks');
    await expect(trackList.aiBadge(trackId)).toContainText('AI');
    const again = await generationApi.importTake(done.id, 1);
    expect([again.status, again.track.id, again.track.origin?.kind, again.track.origin?.sourceTrackId]).toEqual([200, trackId, 'generated', source.id]);
  });

  test('deleting a generation removes its takes and library items; the imported track stays (FR-008)', async ({ api, generationApi, generations }) => {
    requireGeneratorMode('ready', 'oom');
    const done = await restyledTake(generationApi, (await sourceTrack(api)).id, 'Synthwave');
    const take = takeOf(done);
    const imported = await generationApi.importTake(done.id, 1);
    await generations.goto();
    await generations.delete(done.id);
    expect(await generationApi.find(done.id)).toBeUndefined();
    expect((await generationApi.fetch(take.audioUrl)).status()).toBe(404);
    expect((await generationApi.libraryItem(present(take.libraryAssetId, 'library item'))).status).toBe(404);
    expect((await api.getTrack(imported.track.id)).id).toBe(imported.track.id);
  });
});

test.describe('cancel, failures and retry', { tag: '@generative' }, () => {
  test('a running restyle can be cancelled: no take, retry offered, the GPU is free again (US1-2, FR-007, SC-006)', async ({ api, generationApi, generations, trackPage }) => {
    requireGeneratorMode('slow');
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    const queued = await generationApi.restyleOk(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 12 });
    await trackPage.gotoLoaded(track.id);
    await generations.waitForStatus(queued.id, 'running', generationTimeoutMs);
    await generations.part('cancel', queued.id).click();
    await generations.waitForStatus(queued.id, 'cancelled');
    expect((await generationApi.get(queued.id)).takes).toEqual([]);
    await expect(generations.part('retry', queued.id)).toBeVisible();
    const next = await generationApi.restyleOk(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 12 });
    await generationApi.waitForStatus(next.id, ['running', 'completed'], slowGenerationTimeoutMs);
    expect(await generationApi.cancel(next.id)).toBeLessThan(300);
  });

  test('out of memory twice fails with a readable reason; retry makes a new generation (US1-3, FR-012, SC-006)', async ({ api, generationApi, generations, trackPage }) => {
    requireGeneratorMode('oom-twice');
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    const queued = await generationApi.restyleOk(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 12 });
    const failed = await generationApi.waitForStatus(queued.id, terminalStates, generationTimeoutMs);
    expect([failed.status, failed.error?.code]).toEqual(['failed', fake.modes['oom-twice'].restyle.errorCode]);
    await trackPage.gotoLoaded(track.id);
    await expect(generations.part('error', queued.id)).toContainText(new RegExp(fake.modes['oom-twice'].restyle.messagePattern, 'iu'));
    const before = (await generationApi.list({ sourceTrackId: track.id })).map((g) => g.id);
    await generations.part('retry', queued.id).click();
    const retried = await generationApi.waitForNewest({ sourceTrackId: track.id }, before);
    expect((await generationApi.waitForStatus(retried.id, terminalStates, generationTimeoutMs)).status).toBe('failed');
    expect((await api.getTrack(track.id)).status).toBe('completed');
  });

  test('without enough free GPU memory the job waits, then fails "GPU busy" (FR-012, SC-006)', async ({ api, generationApi, generations, trackPage }) => {
    requireGeneratorMode('busy');
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    const queued = await generationApi.restyleOk(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 12 });
    await trackPage.gotoLoaded(track.id);
    await generations.waitForStatus(queued.id, 'waitingForGpu', generationTimeoutMs);
    const failed = await generationApi.waitForStatus(queued.id, terminalStates, generationTimeoutMs);
    expect([failed.status, failed.error?.code]).toEqual(['failed', fake.modes.busy.restyle.errorCode]);
    expect(failed.error?.message).toMatch(new RegExp(fake.modes.busy.restyle.messagePattern, 'u'));
  });

  test('a generator restart during a job ends it as interrupted; retry completes (edge case, SC-006)', { tag: '@restart' }, async ({ api, generationApi }) => {
    requireGeneratorMode('slow');
    requireEnv('E2E_GENERATIVE_COMPOSE_FILES');
    test.setTimeout(slowGenerationTimeoutMs * 2);
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    const queued = await generationApi.restyleOk(track.id, { styleId: jazz.id, mode: 'range', rangeStartSeconds: 0, rangeEndSeconds: 12 });
    await generationApi.waitForStatus(queued.id, ['running'], generationTimeoutMs);
    restartService('generator');
    const failed = await generationApi.waitForStatus(queued.id, terminalStates, generationTimeoutMs);
    expect([failed.status, failed.error?.code]).toEqual(['failed', 'interrupted']);
    const retried = await generationApi.retry(queued.id);
    expect((await generationApi.waitForCompleted(retried.id, slowGenerationTimeoutMs)).takes).toHaveLength(1);
  });
});
