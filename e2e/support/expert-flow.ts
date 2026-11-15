/**
 * The keyboard-only Expert flow of SC-008 / quickstart V-E2, step by step; every key goes through
 * one {@link KeyCounter} (counting rule in `keyboard.ts`).
 *
 * @packageDocumentation
 */
import { expect, type Page } from '@playwright/test';
import { CommandPalette } from '../pages/command-palette';
import type { MixerPage } from '../pages/mixer-page';
import type { StemStudioApi, TrackDto } from './api';
import { isOn, numericValue } from './controls';
import { KeyCounter } from './keyboard';

/** What the flow needs. */
export interface ExpertFlow {
  readonly page: Page;
  readonly api: StemStudioApi;
  readonly mixer: MixerPage;
  readonly keys: KeyCounter;
  readonly palette: CommandPalette;
}

/** Creates the flow objects for a page. */
export function expertFlow(page: Page, api: StemStudioApi, mixer: MixerPage): ExpertFlow {
  const keys = new KeyCounter(page);
  return { page, api, mixer, keys, palette: new CommandPalette(page, keys) };
}

/** Palette -> track -> palette "open mixer" -> mixer ready. */
export async function paletteToMixer(flow: ExpertFlow, track: TrackDto): Promise<void> {
  await flow.palette.run(track.title, `track-${track.id}`);
  await flow.page.waitForURL(`**/tracks/${track.id}`);
  await expect(flow.page.getByTestId('open-mixer')).toBeVisible();
  await flow.palette.runFirst('open mixer');
  await flow.page.waitForURL(`**/tracks/${track.id}/mixer**`);
  await flow.mixer.waitForReady(track.stems.map((stem) => stem.name));
}

/** `→` `[` `→` `→` `]` `L`: loop A at 5 s, B at 15 s, loop on. */
export async function loopWithKeys(flow: ExpertFlow): Promise<void> {
  for (const key of ['ArrowRight', '[', 'ArrowRight', 'ArrowRight', ']', 'l']) {
    await flow.keys.press(key);
  }
  await expectLoop(flow.mixer, 5, 15);
}

/** Asserts loop A/B (seconds, +-0.5) and the loop toggle on. */
export async function expectLoop(mixer: MixerPage, start: number, end: number): Promise<void> {
  await expect.poll(() => numericValue(mixer.byId('loop-start'))).toBeCloseTo(start, 0);
  await expect.poll(() => numericValue(mixer.byId('loop-end'))).toBeCloseTo(end, 0);
  await expect.poll(() => isOn(mixer.byId('loop-toggle'))).toBe(true);
}

/** `Mod+S` -> name -> Enter; returns the new mix id from the API. */
export async function saveWithKeys(flow: ExpertFlow, trackId: string, name: string): Promise<string> {
  await flow.keys.press('ControlOrMeta+S');
  await expect(flow.page.getByTestId('mix-name-input')).toBeFocused();
  await flow.keys.type(name);
  await flow.keys.press('Enter');
  await expect(flow.mixer.byId('unsaved-indicator')).toBeHidden();
  await expect.poll(async () => (await flow.api.findMix(trackId, name))?.id).toBeTruthy();
  return (await flow.api.findMix(trackId, name))?.id ?? '';
}

/** `G` `M` -> mix library -> palette loads the mix -> its loop is restored. */
export async function reloadMixWithKeys(flow: ExpertFlow, mixId: string, name: string): Promise<void> {
  await flow.keys.press('g');
  await flow.keys.press('m');
  await flow.page.waitForURL('**/mixes');
  await expect(flow.page.getByTestId(`mix-row-${mixId}`)).toBeVisible();
  await flow.palette.run(name, `mix-${mixId}`);
  await flow.page.waitForURL(`**/mixer?mixId=${mixId}`);
  await expectLoop(flow.mixer, 5, 15);
}
