/**
 * Shared knowledge of the device specs (feature 003): the fake device of the stack, fresh imports
 * whose jobs really ran (cached results carry no device, data-model 2.2) and device assertions.
 *
 * @packageDocumentation
 */
import { expect, type Locator } from '@playwright/test';
import type { JobDeviceDto, ProcessingDeviceKind, StemStudioApi, TrackDto } from './api';
import { uniqueBand } from './detail';
import { detailPhaseTimeoutMs, fakeDeviceMode, fakeJobTimeoutMs, type FakeDeviceMode } from './env';
import { test } from './test';

/** GPU name the fake device runner reports (stem-separation T015, data-model 1). */
export const fakeGpuName = 'Fake GPU';

/** Device text of the fake GPU (`JobDeviceDto.DisplayText()`, ui-test-ids 1.2.0). */
export const fakeGpuText = `GPU (${fakeGpuName})`;

/** Fallback reason pattern of an out-of-memory retry on the CPU (data-model 2.3). */
export const outOfMemoryReason = /ran out of memory/i;

/** Test timeout for a fresh import whose standard and detail jobs both have to finish. */
export const freshImportTimeoutMs = fakeJobTimeoutMs + detailPhaseTimeoutMs + 30_000;

/** What a `track-device` / `detail-device` badge must show. */
export interface ExpectedJobDevice {
  /** `data-device`; omitted when the spec does not pin the kind (fallback runs). */
  readonly kind?: ProcessingDeviceKind;
  /** `data-fallback`. */
  readonly fallback: boolean;
  /** Text the badge contains (for example "GPU (Fake GPU)"). */
  readonly text?: string;
  /** Reason in the fallback notice; when omitted the notice must not exist. */
  readonly reason?: RegExp;
}

/**
 * Runs the current test only on a stack whose fake device is one of `modes` and skips it
 * otherwise. On the default `cpu` stack a GPU test fails with a hint, so a `@gpu` run against the
 * wrong stack never passes vacuously.
 */
export function requireGpuStack(modes: readonly FakeDeviceMode[]): void {
  const hint = 'start the stack with STEMSTUDIO_FAKE_DEVICE=gpu or gpu-oom and set the same value for the suite';
  expect(fakeDeviceMode, hint).not.toBe('cpu');
  test.skip(!modes.includes(fakeDeviceMode), `needs STEMSTUDIO_FAKE_DEVICE=${modes.join(' or ')}`);
}

/** Skips the current test unless the stack runs with the default fake device `cpu`. */
export function requireCpuStack(): void {
  test.skip(fakeDeviceMode !== 'cpu', 'needs the default stack (STEMSTUDIO_FAKE_DEVICE=cpu)');
}

/**
 * Uploads a per-run copy of `detail_10s_band.wav` (new audio hash, so neither the standard nor the
 * detail result comes from the worker cache) and waits until its automatic detail phase completed.
 */
export async function importFreshBand(api: StemStudioApi, salt: string): Promise<TrackDto> {
  const track = await api.ensureFileCompleted(uniqueBand(`device-${salt}`), fakeJobTimeoutMs);
  const done = await api.waitForDetail(track.id, ['completed', 'failed', 'cancelled'], detailPhaseTimeoutMs);
  expect(done.detail.status, done.detail.error ?? '').toBe('completed');
  return done;
}

/** Asserts a job device record of `GET /api/tracks/{id}` (server state behind the badge). */
export function expectJobDeviceRecord(device: JobDeviceDto | null | undefined, expected: ExpectedJobDevice): void {
  expect(device, 'job device record').toBeTruthy();
  expect(device?.cpuFallback).toBe(expected.fallback);
  if (expected.kind !== undefined) {
    expect(device?.kind).toBe(expected.kind);
  }
}

/**
 * Asserts a device badge and its fallback notice (ui-test-ids 1.2.0): `data-device`,
 * `data-fallback`, optional text, and the notice with the reason only when one is expected.
 */
export async function expectJobDevice(badge: Locator, notice: Locator, expected: ExpectedJobDevice): Promise<void> {
  if (expected.kind !== undefined) {
    await expect(badge).toHaveAttribute('data-device', expected.kind);
  }
  await expect(badge).toHaveAttribute('data-fallback', String(expected.fallback));
  if (expected.text !== undefined) {
    await expect(badge).toContainText(expected.text);
  }
  await expectNotice(notice, expected.reason);
}

/** The notice contains `reason`, or does not exist when `reason` is undefined. */
async function expectNotice(notice: Locator, reason: RegExp | undefined): Promise<void> {
  if (reason === undefined) {
    await expect(notice).toHaveCount(0);
  } else {
    await expect(notice).toContainText(reason);
  }
}
