/**
 * The screens of the feature 004 matrices (SC-001, SC-005): how to open each one with the seeded
 * data and when it is ready to be scanned or captured.
 *
 * @packageDocumentation
 */
import { expect, type Page } from '@playwright/test';
import type { UxData } from './ux-data';

/** Screen states covered by the visual and accessibility matrices. */
export type ScreenId = 'import' | 'tracks' | 'track' | 'track-no-detail' | 'mixer' | 'mixer-expanded' | 'mixes';

/** Every screen of the matrices, in report order. */
export const screenIds: readonly ScreenId[] = [
  'import',
  'tracks',
  'track',
  'track-no-detail',
  'mixer',
  'mixer-expanded',
  'mixes',
];

/** Help screen topic per screen (design-spec.md 7; V-H1). */
export const screenTopics: Readonly<Record<ScreenId, string>> = {
  import: 'import-screen',
  tracks: 'tracks-library',
  track: 'track-screen',
  'track-no-detail': 'track-screen',
  mixer: 'mixer-screen',
  'mixer-expanded': 'mixer-screen',
  mixes: 'mixes-library',
};

interface ScreenDefinition {
  readonly path: (data: UxData) => string;
  readonly ready: (page: Page, data: UxData) => Promise<void>;
}

const screens: Readonly<Record<ScreenId, ScreenDefinition>> = {
  import: { path: () => '/', ready: importReady },
  tracks: { path: () => '/tracks', ready: tracksReady },
  track: { path: (d) => `/tracks/${d.detailTrack.id}`, ready: (p) => trackReady(p, 'completed') },
  'track-no-detail': { path: (d) => `/tracks/${d.plainTrack.id}`, ready: (p, d) => trackReady(p, d.plainTrack.detail.status) },
  mixer: { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: mixerReady },
  'mixer-expanded': { path: (d) => `/tracks/${d.detailTrack.id}/mixer`, ready: mixerExpandedReady },
  mixes: { path: () => '/mixes', ready: (p, d) => expect(p.getByTestId(`mix-row-${d.mixId}`)).toBeVisible() },
};

/** Opens a screen, waits for the shell and the screen's content, then for the web fonts. */
export async function openScreen(page: Page, id: ScreenId, data: UxData): Promise<void> {
  const screen = screens[id];
  await page.goto(screen.path(data));
  await expect(page.getByTestId('app-shell')).toHaveAttribute('data-level', /^(beginner|advanced|expert)$/);
  await screen.ready(page, data);
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
}

async function importReady(page: Page): Promise<void> {
  await expect(page.getByTestId('legal-notice')).toBeVisible();
  await expect(page.getByTestId('worker-device')).toHaveAttribute('data-device', /^(cpu|gpu|unknown)$/);
}

async function tracksReady(page: Page, data: UxData): Promise<void> {
  await expect(page.getByTestId(`track-row-${data.detailTrack.id}`)).toBeVisible();
  await expect(page.getByTestId(`track-row-${data.plainTrack.id}`)).toBeVisible();
}

async function trackReady(page: Page, detailStatus: string): Promise<void> {
  await expect(page.getByTestId('track-status')).toHaveText(/completed/i);
  await expect(page.getByTestId('detail-status')).toHaveText(detailStatus);
}

async function mixerReady(page: Page): Promise<void> {
  await expect(page.getByTestId('strip-vocals')).toBeVisible();
  await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
}

async function mixerExpandedReady(page: Page): Promise<void> {
  await mixerReady(page);
  await page.getByTestId('expand-drums').click();
  await expect(page.getByTestId('expand-drums')).toHaveAttribute('aria-expanded', 'true', { timeout: 30_000 });
  await expect(page.getByTestId('strip-drums-kick')).toBeVisible();
  // The re-layout leaves the pointer over the drums Mute/Solo, whose help tooltip would then be captured.
  await page.mouse.move(0, 0);
  await expect(page.getByTestId(/^help-tooltip-/)).toHaveCount(0);
}
