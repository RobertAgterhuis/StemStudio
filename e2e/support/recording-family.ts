/**
 * The US1 take case of one instrument family (feature 015 T044; FR-001..FR-006, FR-012, FR-017; SC-007, SC-008). The
 * fake capture file is a launch option, and Playwright allows worker-scoped options such as `launchOptions` only at the
 * top level of a spec file (a `test.use` in a describe group would force a new worker). So each family has its own spec
 * file `tests/recording-family-<family>.spec.ts` that calls {@link recordingFamilySpec} once at its top level.
 *
 * @packageDocumentation
 */
import type { TrackDto } from './api';
import { fakeStems, silentDb } from './detail';
import { fakeJobTimeoutMs } from './env';
import { requireChromium } from './feature-skips';
import {
  expectOnlyAppOrigins,
  flacBitDepth,
  recordingFamilies,
  type RecordingFamily,
  recordingFixturePath,
  recordingLaunchOptions,
  takeReadyTimeoutMs,
  watchOrigins,
} from './recording';
import { expect, test } from './recording-test';

/**
 * Declares the launch options, hooks and the take case of `familyName` in the calling spec file: record 10 s over the
 * mix, the take row goes `local` -> `ready`, the take plays in the strip and is stored as a 24-bit FLAC, and only the web
 * and API origins are contacted.
 *
 * @param familyName - One of the `family` names of {@link recordingFamilies}.
 */
export function recordingFamilySpec(familyName: string): void {
  const entry = recordingFamilies.find((candidate) => candidate.family === familyName);
  if (entry === undefined) {
    throw new Error(`unknown recording family ${familyName}`);
  }
  // Playwright reads the fixtures of an option from a destructuring pattern, so the empty one is required.
  // eslint-disable-next-line no-empty-pattern
  test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions(entry.input)), { scope: 'worker' }], permissions: ['microphone'] });
  const track = trackHooks();
  test.describe(`recording ${entry.family}`, { tag: '@recording' }, () => {
    familyTakeCase(entry, track);
  });
}

/** Imports the song once per test and deletes its takes afterwards; the returned function gives the track. */
function trackHooks(): () => TrackDto {
  let track: TrackDto | undefined;
  test.beforeEach(async ({ api, browserName }) => {
    requireChromium(browserName);
    test.setTimeout(fakeJobTimeoutMs + 120_000);
    track = await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
  });
  test.afterEach(async ({ recordingApi }) => {
    if (track !== undefined) {
      await recordingApi.deleteAllTakes(track.id);
    }
  });
  return () => {
    if (track === undefined) {
      throw new Error('the track of the recording family case is not imported');
    }
    return track;
  };
}

function familyTakeCase({ family, part, input, channels }: RecordingFamily, track: () => TrackDto): void {
  test(`${family}: a 10 s take is saved as 24-bit FLAC and plays in the take strip (US1, SC-008)`, async ({
    page, mixer, recording, recordingApi, assetsApi,
  }) => {
    recordingFixturePath(input);
    const origins = watchOrigins(page);
    await mixer.goto(track().id);
    await mixer.waitForReady(fakeStems);
    await recording.openWithInput();
    await recording.choosePart(part);
    await recording.chooseCountIn(0);
    await recording.recordFor(10);

    await expect(recording.strip).toBeVisible({ timeout: 2_000 });
    const takeId = await recording.newestSavedTakeId();
    await recording.waitForRow(takeId, 'ready', takeReadyTimeoutMs);
    const take = await recordingApi.getTake(takeId);
    expect([take.part, take.inputChannels]).toEqual([part, channels]);
    expect(take.durationSeconds).toBeGreaterThan(9.5);
    expect(take.durationSeconds).toBeLessThan(11);
    const asset = await assetsApi.waitForReady(take.assetId, takeReadyTimeoutMs);
    expect([asset.audio?.format, asset.audio?.bitDepth]).toEqual(['flac', 24]);
    expect(flacBitDepth((await assetsApi.download(take.assetId)).bytes)).toBe(24);

    await expect(recording.strip).toHaveAttribute('data-take-id', takeId);
    await mixer.seek(take.startSample / 44_100);
    await mixer.play();
    await expect.poll(() => mixer.peak('take'), { timeout: 10_000 }).toBeGreaterThan(silentDb);
    expectOnlyAppOrigins(origins);
  });
}
