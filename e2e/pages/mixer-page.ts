import { expect, type Locator, type Page } from '@playwright/test';
import type { StemName } from '../support/api';
import { fillAndCommit, isOn, numericValue, peakDb, setNumericValue, setToggle } from '../support/controls';

/** Settings the suite applies and verifies; faders/master in %, pan -100..100 (UI units). */
export interface MixerSettings {
  readonly faders: Partial<Record<StemName, number>>;
  readonly pans: Partial<Record<StemName, number>>;
  readonly mutes: readonly StemName[];
  readonly solos: readonly StemName[];
  readonly master: number;
  readonly metronome: { readonly enabled: boolean; readonly bpm: number; readonly offsetMs: number };
  readonly loop: { readonly start: number; readonly end: number; readonly enabled: boolean };
}

/** Mixer `/tracks/{id}/mixer` (ui-test-ids.md, "Mixer"). */
export class MixerPage {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Element with the given contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Channel strip of a stem. */
  strip(stem: StemName): Locator {
    return this.byId(`strip-${stem}`);
  }

  /** Volume fader of a stem (`aria-valuenow` 0-100). */
  fader(stem: StemName): Locator {
    return this.byId(`fader-${stem}`);
  }

  /** Pan control of a stem (`aria-valuenow` -100..100). */
  pan(stem: StemName): Locator {
    return this.byId(`pan-${stem}`);
  }

  /** Mute toggle of a stem. */
  mute(stem: StemName): Locator {
    return this.byId(`mute-${stem}`);
  }

  /** Solo toggle of a stem. */
  solo(stem: StemName): Locator {
    return this.byId(`solo-${stem}`);
  }

  /** Meter of a stem or of the master bus (engine writes `data-peak-db`). */
  meter(target: StemName | 'master'): Locator {
    return this.byId(`meter-${target}`);
  }

  /** Opens the mixer of a track, optionally loading a saved mix (`?mixId=`). */
  async goto(trackId: string, mixId?: string): Promise<void> {
    await this.page.goto(`/tracks/${trackId}/mixer${mixId === undefined ? '' : `?mixId=${mixId}`}`);
  }

  /** Waits until the strips of `stems` are rendered and play is enabled (stems decoded). */
  async waitForReady(stems: readonly StemName[]): Promise<void> {
    for (const stem of stems) {
      await expect(this.strip(stem)).toBeVisible();
    }
    await expect(this.byId('transport-play')).toBeEnabled({ timeout: 60_000 });
  }

  /** Presses play (the user gesture that unlocks audio). */
  async play(): Promise<void> {
    await this.byId('transport-play').click();
  }

  /** Current peak level of a meter in dBFS. */
  async peak(target: StemName | 'master'): Promise<number> {
    return peakDb(this.meter(target));
  }

  /** Sets loop A/B (seconds) and the loop toggle. */
  async setLoop(start: number, end: number, enabled: boolean): Promise<void> {
    await fillAndCommit(this.byId('loop-start'), String(start));
    await fillAndCommit(this.byId('loop-end'), String(end));
    await setToggle(this.byId('loop-toggle'), enabled);
  }

  /** Applies every setting of `settings` through the UI. */
  async apply(settings: MixerSettings): Promise<void> {
    await this.applyChannels(settings);
    await setNumericValue(this.byId('master-fader'), settings.master);
    await fillAndCommit(this.byId('metronome-bpm'), String(settings.metronome.bpm));
    await fillAndCommit(this.byId('metronome-offset'), String(settings.metronome.offsetMs));
    await setToggle(this.byId('metronome-toggle'), settings.metronome.enabled);
    await this.setLoop(settings.loop.start, settings.loop.end, settings.loop.enabled);
  }

  private async applyChannels(settings: MixerSettings): Promise<void> {
    for (const [stem, value] of entries(settings.faders)) {
      await setNumericValue(this.fader(stem), value);
    }
    for (const [stem, value] of entries(settings.pans)) {
      await setNumericValue(this.pan(stem), value);
    }
    for (const stem of settings.mutes) {
      await setToggle(this.mute(stem), true);
    }
    for (const stem of settings.solos) {
      await setToggle(this.solo(stem), true);
    }
  }

  /** Asserts that the UI shows exactly `settings` for the given stems. */
  async expectSettings(settings: MixerSettings, stems: readonly StemName[]): Promise<void> {
    for (const stem of stems) {
      await this.expectChannel(settings, stem);
    }
    await expect.poll(() => numericValue(this.byId('master-fader'))).toBeCloseTo(settings.master, 0);
    await expect(this.byId('metronome-bpm')).toHaveValue(String(settings.metronome.bpm));
    await expect(this.byId('metronome-offset')).toHaveValue(String(settings.metronome.offsetMs));
    await expect.poll(() => isOn(this.byId('metronome-toggle'))).toBe(settings.metronome.enabled);
    await expect(this.byId('loop-start')).toHaveValue(String(settings.loop.start));
    await expect(this.byId('loop-end')).toHaveValue(String(settings.loop.end));
    await expect.poll(() => isOn(this.byId('loop-toggle'))).toBe(settings.loop.enabled);
  }

  private async expectChannel(settings: MixerSettings, stem: StemName): Promise<void> {
    await expect.poll(() => numericValue(this.fader(stem))).toBeCloseTo(settings.faders[stem] ?? 80, 0);
    await expect.poll(() => numericValue(this.pan(stem))).toBeCloseTo(settings.pans[stem] ?? 0, 0);
    await expect.poll(() => isOn(this.mute(stem))).toBe(settings.mutes.includes(stem));
    await expect.poll(() => isOn(this.solo(stem))).toBe(settings.solos.includes(stem));
  }

  /** First save of a new mix: `mix-save` opens the name dialog, which gets `name`. */
  async saveNew(name: string): Promise<void> {
    await this.byId('mix-save').click();
    await this.confirmName(name);
    await expect(this.byId('unsaved-indicator')).toBeHidden();
  }

  /** Saves changes to the loaded mix (PUT, no dialog). */
  async saveExisting(): Promise<void> {
    await this.byId('mix-save').click();
    await expect(this.byId('unsaved-indicator')).toBeHidden();
  }

  /** "Save as" with a new name. */
  async saveAs(name: string): Promise<void> {
    await this.byId('mix-save-as').click();
    await this.confirmName(name);
    await expect(this.byId('unsaved-indicator')).toBeHidden();
  }

  private async confirmName(name: string): Promise<void> {
    await this.byId('mix-name-input').fill(name);
    await this.byId('mix-save-confirm').click();
    await expect(this.byId('mix-error')).toBeHidden();
  }
}

function entries(record: Partial<Record<StemName, number>>): [StemName, number][] {
  return Object.entries(record) as [StemName, number][];
}
