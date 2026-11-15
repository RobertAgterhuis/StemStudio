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
import { positionSeconds } from './levels';

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

/**
 * `→` `[` `→` `→` `]` `L`: loop A at 5 s, B at 15 s, loop on. Each key waits for its effect before the
 * next one (at human speed; key presses sent with no gap overtake each other on the circuit).
 */
export async function loopWithKeys(flow: ExpertFlow): Promise<void> {
  const position = (): Promise<number> => transportSeconds(flow.mixer);
  const steps: readonly (readonly [string, () => Promise<number>, number])[] = [
    ['ArrowRight', position, 5],
    ['[', () => numericValue(flow.mixer.byId('loop-start')), 5],
    ['ArrowRight', position, 10],
    ['ArrowRight', position, 15],
    [']', () => numericValue(flow.mixer.byId('loop-end')), 15],
  ];
  for (const [key, read, expected] of steps) {
    await flow.keys.press(key);
    await expect.poll(read).toBeCloseTo(expected, 0);
  }
  await flow.keys.press('l');
  await expectLoop(flow.mixer, 5, 15);
}

async function transportSeconds(mixer: MixerPage): Promise<number> {
  return positionSeconds((await mixer.byId('transport-position').textContent()) ?? '');
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
