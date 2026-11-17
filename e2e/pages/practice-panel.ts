import { expect, type Locator, type Page } from '@playwright/test';
import { fillAndCommit, numericValue, setNumericValue, setToggle } from '../support/controls';

/** `data-stretch` of `practice-panel` (engine `PracticeStatus.stretch`). */
export type StretchState = 'bypass' | 'loading' | 'active' | 'unavailable';

/** Trainer settings as typed into the `trainer-*` inputs. */
export interface TrainerInputs {
  readonly start: number;
  readonly step: number;
  readonly target: number;
  readonly passes: number;
}

const maxClicks = 30;

/**
 * Practice tempo panel of the mixer (ui-test-ids 1.11.0, feature 012): speed, transpose, reset,
 * effective tempo, quality hint, stretch status, count-in and speed trainer.
 */
export class PracticePanel {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** The region (`data-stretch`, `data-speed`, `data-transpose`). */
  get panel(): Locator {
    return this.byId('practice-panel');
  }

  /** Any element of the panel (or the mixer) by its contract id. */
  byId(testId: string): Locator {
    return this.page.getByTestId(testId);
  }

  /** Speed slider 50-120. */
  get speed(): Locator {
    return this.byId('practice-speed');
  }

  /** Transpose spinbutton -12..12. */
  get transpose(): Locator {
    return this.byId('practice-transpose');
  }

  /** "Original speed and key". */
  get reset(): Locator {
    return this.byId('practice-reset');
  }

  /** Count-in switch. */
  get countInToggle(): Locator {
    return this.byId('countin-toggle');
  }

  /** Count-in indicator (exists only while a count-in runs). */
  get countInIndicator(): Locator {
    return this.byId('countin-indicator');
  }

  /** Speed trainer switch (Advanced, Expert). */
  get trainerToggle(): Locator {
    return this.byId('trainer-toggle');
  }

  /** Trainer status (`data-state`, `data-pass`, `data-speed`). */
  get trainerStatus(): Locator {
    return this.byId('trainer-status');
  }

  /** The speed heard now (`data-speed` of the panel). */
  async heardSpeed(): Promise<number> {
    return Number(await this.panel.getAttribute('data-speed'));
  }

  /** The transpose now (`data-transpose` of the panel). */
  async heardTranspose(): Promise<number> {
    return Number(await this.panel.getAttribute('data-transpose'));
  }

  /** Waits until the panel reports `state` (the module loads on the first non-neutral change). */
  async waitForStretch(state: StretchState, timeout = 15_000): Promise<void> {
    await expect(this.panel).toHaveAttribute('data-stretch', state, { timeout });
  }

  /**
   * Sets the speed with the keyboard (Home = 50, End = 120, arrow keys in 1 % steps) and waits
   * until the slider shows it.
   */
  async setSpeed(percent: number): Promise<void> {
    const edge = edgeKey(percent);
    if (edge === undefined) {
      await setNumericValue(this.speed, percent);
    } else {
      await this.speed.focus();
      await this.speed.press(edge);
    }
    await expect.poll(() => numericValue(this.speed)).toBe(percent);
  }

  /** Clicks `practice-transpose-up`/`-down` until the panel reports `semitones`. */
  async setTranspose(semitones: number): Promise<void> {
    for (let click = 0; click < maxClicks; click++) {
      const now = await this.heardTranspose();
      if (now === semitones) return;
      await this.byId(now < semitones ? 'practice-transpose-up' : 'practice-transpose-down').click();
      await expect.poll(() => this.heardTranspose()).not.toBe(now);
    }
    expect(await this.heardTranspose()).toBe(semitones);
  }

  /** Speed and transpose in one call (speed first). */
  async set(speedPercent: number, transposeSemitones: number): Promise<void> {
    await this.setSpeed(speedPercent);
    await this.setTranspose(transposeSemitones);
  }

  /** Count-in on with `bars` (Advanced/Expert: `countin-bars`) or off with 0. */
  async setCountIn(bars: 0 | 1 | 2, everyPass = false): Promise<void> {
    await setToggle(this.countInToggle, bars > 0);
    if (bars === 0) return;
    await this.byId('countin-bars').selectOption(String(bars));
    await setToggle(this.byId('countin-every-pass'), everyPass);
  }

  /** Types the trainer settings (each committed with Tab) and switches the trainer on or off. */
  async setTrainer(inputs: TrainerInputs, enabled: boolean): Promise<void> {
    await fillAndCommit(this.byId('trainer-start'), String(inputs.start));
    await fillAndCommit(this.byId('trainer-step'), String(inputs.step));
    await fillAndCommit(this.byId('trainer-target'), String(inputs.target));
    await fillAndCommit(this.byId('trainer-passes'), String(inputs.passes));
    await setToggle(this.trainerToggle, enabled);
  }

  /** Asserts the trainer inputs show `inputs`. */
  async expectTrainerInputs(inputs: TrainerInputs): Promise<void> {
    await expect(this.byId('trainer-start')).toHaveValue(String(inputs.start));
    await expect(this.byId('trainer-step')).toHaveValue(String(inputs.step));
    await expect(this.byId('trainer-target')).toHaveValue(String(inputs.target));
    await expect(this.byId('trainer-passes')).toHaveValue(String(inputs.passes));
  }
}

function edgeKey(percent: number): 'Home' | 'End' | undefined {
  if (percent === 50) return 'Home';
  return percent === 120 ? 'End' : undefined;
}
