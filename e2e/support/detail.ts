/**
 * Shared knowledge of the `@detail` specs (feature 002): the fixed outcome table of the fake
 * detail models, per-run unique band audio, and set-up and assertion helpers.
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import type { DetailSection } from '../pages/detail-section';
import type {
  DetailPhaseStatus,
  DetailStemName,
  ParentOutcomeKind,
  ParentStemName,
  StemName,
  StemStudioApi,
  TrackDto,
} from './api';
import { detailPhaseTimeoutMs, fakeDetailStepSeconds, fakeJobTimeoutMs, runId } from './env';
import { fixturePath } from './fixtures';
import { oneOf } from './text';
import { uniqueWavCopy } from './unique-audio';

/** Expected outcome of one parent: the kind and the detail stems in display order (`rest` last). */
export interface ExpectedOutcome {
  readonly outcome: ParentOutcomeKind;
  readonly stems: readonly DetailStemName[];
}

/**
 * Outcome table of the fake detail models (stem-separation T042) on any input, because the fake
 * standard separator copies the mix into every stem: drums kick 0.5x, snare 0.3x, toms 0.2x;
 * vocals lead 0.7x, backing 0.3x; guitar one audible family; piano outputs summing to 0.3x;
 * other brass 0.6x, strings 0.4x (display order strings before brass).
 */
export const fakeDetailOutcomes: Readonly<Record<StemName, ExpectedOutcome>> = {
  vocals: { outcome: 'split', stems: ['lead', 'backing', 'rest'] },
  drums: { outcome: 'split', stems: ['kick', 'snare', 'toms', 'rest'] },
  bass: { outcome: 'not-supported', stems: [] },
  guitar: { outcome: 'nothing-further', stems: [] },
  piano: { outcome: 'not-reliable', stems: [] },
  other: { outcome: 'split', stems: ['strings', 'brass', 'rest'] },
};

/** The splittable parents (the ones with a `detail-outcome-<parent>` id). */
export const parentStems: readonly ParentStemName[] = ['vocals', 'drums', 'guitar', 'piano', 'other'];

/** Parents the fake table splits (they get an `expand-<parent>` button). */
export const splitParents: readonly ParentStemName[] = ['vocals', 'drums', 'other'];

/** Statuses of a detail phase that is still active. */
export const activeStatuses: readonly DetailPhaseStatus[] = ['queued', 'running'];

/** User texts per outcome kind (backend-api 1.2.0 `ParentOutcomeKind`, data-model 2.2). */
export const outcomeText: Readonly<Record<ParentOutcomeKind, RegExp>> = {
  split: /split into/i,
  'skipped-silent': /skipped.*silent/i,
  'nothing-further': /nothing further to separate/i,
  'not-reliable': /not reliable/i,
  'not-supported': /not split/i,
};

/** A copy of `detail_10s_band.wav` the stack has not seen in this run (live detail phase). */
export function uniqueBand(salt: string): string {
  return uniqueWavCopy(fixturePath('detail_10s_band.wav'), `${runId()}-${salt}`);
}

/** Outcome table of a track as returned by the API (outcome + detail stem names per parent). */
export function outcomesOf(track: TrackDto): Partial<Record<StemName, ExpectedOutcome>> {
  return Object.fromEntries(
    track.detail.parents.map((p) => [p.parent, { outcome: p.outcome, stems: p.stems.map((s) => s.name) }]),
  );
}

/** Kept detail stems of a split parent (the table's stems without `rest`). */
export function keptStems(parent: StemName): DetailStemName[] {
  return fakeDetailOutcomes[parent].stems.filter((name) => name !== 'rest');
}

/** Asserts the track page shows the fake outcome table: kind, text and listed detail stems. */
export async function expectFakeOutcomesShown(detail: DetailSection): Promise<void> {
  for (const parent of parentStems) {
    const expected = fakeDetailOutcomes[parent];
    await expect(detail.outcome(parent)).toHaveAttribute('data-outcome', expected.outcome);
    await expect(detail.outcome(parent)).toHaveText(outcomeText[expected.outcome]);
    for (const stem of keptStems(parent)) {
      await expect(detail.outcome(parent)).toContainText(stem);
      await expect(detail.stem(parent, stem)).toBeVisible();
    }
  }
}

/**
 * The plain band fixture with a completed detail phase (fast on reruns: worker cache). Starts the
 * phase again if an earlier run left it failed or cancelled.
 */
export async function ensureDetailCompleted(api: StemStudioApi): Promise<TrackDto> {
  const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
  const settled = await api.waitForDetail(track.id, ['completed', 'failed', 'cancelled', 'none'], detailPhaseTimeoutMs);
  if (settled.detail.status !== 'completed') {
    await api.startDetail(track.id);
  }
  const done = await api.waitForDetail(track.id, ['completed', 'failed'], detailPhaseTimeoutMs);
  expect(done.detail.status, done.detail.error ?? '').toBe('completed');
  return done;
}

/** Imports a unique band copy through the API and waits until its detail phase is active. */
export async function importWithLivePhase(api: StemStudioApi, salt: string): Promise<TrackDto> {
  const track = await api.ensureFileCompleted(uniqueBand(salt), fakeJobTimeoutMs);
  return api.waitForDetail(track.id, activeStatuses, detailPhaseTimeoutMs);
}

/** Fails with a hint when a phase that should still be active has already finished. */
export function expectStillActive(track: TrackDto, what: string): void {
  const hint = `${what}: the detail phase ended too early; start the stack with STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS=${String(fakeDetailStepSeconds)}`;
  expect(activeStatuses, hint).toContain(track.detail.status);
}

/**
 * Samples `detail-progress` once per second until the phase leaves `queued`/`running`; returns
 * the values seen (used for "progress never decreases").
 */
export async function sampleProgressUntilDone(detail: DetailSection): Promise<number[]> {
  const values: number[] = [];
  const done = oneOf(['completed', 'failed', 'cancelled']);
  await expect
    .poll(
      async () => {
        values.push(await detail.progressValue());
        return (await detail.status.textContent()) ?? '';
      },
      { timeout: detailPhaseTimeoutMs, intervals: [1000] },
    )
    .toMatch(done);
  return values.filter((v) => !Number.isNaN(v));
}
