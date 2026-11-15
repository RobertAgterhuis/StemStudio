import { expect, type Locator } from '@playwright/test';
import { setNumericValue } from '../support/controls';
import { applySteps, TestIdPage } from './test-id-page';
import { setBlazorInputFiles } from '../support/input-file';

/** Choices of the song page; unset fields keep the page's defaults. */
export interface SongChoices {
  readonly lyrics?: string;
  readonly title?: string;
  /** Visible label of the language option, e.g. "English". */
  readonly language?: string;
  readonly style?: string;
  readonly leadVoice?: 'female' | 'male' | 'any';
  readonly instrumental?: boolean;
  readonly durationSeconds?: number;
}

/** The song page `/songs/new` (ui-test-ids 1.9.0 `song-*`). */
export class SongPage extends TestIdPage {
  /** The lyrics textarea. */
  get lyricsText(): Locator {
    return this.byId('song-lyrics-text');
  }

  /** "n / 4096" character counter. */
  get lyricsCount(): Locator {
    return this.byId('song-lyrics-count');
  }

  /** The "Generate" button. */
  get generateButton(): Locator {
    return this.byId('song-generate');
  }

  /** Readable error (`data-field` for field errors). */
  get error(): Locator {
    return this.byId('song-error');
  }

  /** Density warning (> 4 words per second). */
  get densityWarning(): Locator {
    return this.byId('song-density-warning');
  }

  /** Personal use, AI voice label, own lyrics only, no artist imitation. */
  get notice(): Locator {
    return this.byId('song-personal-use-notice');
  }

  /** The song generations list (rows use the 1.8.0 ids). */
  get generations(): Locator {
    return this.byId('song-generations');
  }

  /** Opens `/songs/new`, optionally with a saved lyrics sheet preselected. */
  async goto(lyricsId?: string): Promise<void> {
    await this.page.goto(lyricsId === undefined ? '/songs/new' : `/songs/new?lyricsId=${lyricsId}`);
    await expect(this.lyricsText).toBeVisible();
  }

  /** Applies `choices` (Advanced/Expert) without generating. */
  async fill(choices: SongChoices): Promise<void> {
    const c = choices;
    await applySteps([
      [c.lyrics, () => this.lyricsText.fill(c.lyrics ?? '')],
      [c.title, () => this.byId('song-title-input').fill(c.title ?? '')],
      [c.language, () => this.byId('song-language-select').selectOption({ label: c.language ?? '' })],
      [c.style, () => this.byId('song-style-select').selectOption({ label: c.style ?? '' })],
      [c.leadVoice, () => this.byId('song-lead-voice').selectOption(c.leadVoice ?? 'any')],
      [c.instrumental, () => this.byId('song-instrumental').setChecked(c.instrumental ?? false)],
      [c.durationSeconds, () => setNumericValue(this.byId('song-duration'), c.durationSeconds ?? 120)],
    ]);
  }

  /** "Add sections" and waits until the text has a section marker. */
  async proposeSections(): Promise<void> {
    await this.byId('song-sections-propose').click();
    await expect(this.lyricsText).toHaveValue(/\[(verse|chorus)/iu);
  }

  /** Uploads a lyrics file into the editor. */
  async upload(file: string): Promise<void> {
    await setBlazorInputFiles(this.byId('song-lyrics-upload'), file);
  }

  /** Clicks "Generate". */
  async generate(): Promise<void> {
    await this.generateButton.click();
  }
}
