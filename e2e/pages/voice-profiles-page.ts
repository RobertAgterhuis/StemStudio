import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** Recorder timer text `m:ss`. */
const timerPattern = /(\d+):(\d\d)/u;

/** Longest recording wait (the recorder stops by itself at 60 s). */
const recordTimeoutMs = 90_000;

/** What the "new voice profile" panel is filled with. */
export interface VoiceProfileEntry {
  readonly displayName: string;
  readonly personName: string;
  readonly consent: boolean;
}

/** The voice profiles page `/voices` with recorder and consent (ui-test-ids 1.9.0 `voice-*`). */
export class VoiceProfilesPage extends TestIdPage {
  /** Recorder timer text (`m:ss`). */
  get timer(): Locator {
    return this.byId('voice-record-timer');
  }

  /** Notice when the microphone is unavailable or permission was denied. */
  get recordUnavailable(): Locator {
    return this.byId('voice-record-unavailable');
  }

  /** The consent statement (`data-version`). */
  get consentText(): Locator {
    return this.byId('voice-consent-text');
  }

  /** "Save" (enabled only with a sample, the ticked checkbox and both names). */
  get saveButton(): Locator {
    return this.byId('voice-save');
  }

  /** Readable reason of a refused save. */
  get error(): Locator {
    return this.byId('voice-error');
  }

  /** Opens `/voices` and the "new voice profile" panel. */
  async gotoNew(): Promise<void> {
    await this.page.goto('/voices');
    await this.byId('voice-new').click();
    await expect(this.consentText).toBeVisible();
  }

  /** The row of a profile. */
  row(profileId: string): Locator {
    return this.byId(`voice-row-${profileId}`);
  }

  /** `voice-<part>-<id>`: `play` (`<audio>`), `consent` (details), `delete`. */
  part(part: 'play' | 'consent' | 'delete', profileId: string): Locator {
    return this.byId(`voice-${part}-${profileId}`);
  }

  /** Uploads a sample file. */
  async upload(file: string): Promise<void> {
    await this.byId('voice-upload').setInputFiles(file);
    await expect(this.byId('voice-preview')).toBeVisible();
  }

  /** Records until the timer shows at least `seconds`, then stops. */
  async record(seconds: number, timeoutMs = recordTimeoutMs): Promise<void> {
    await this.byId('voice-record-start').click();
    await expect.poll(this.timerSeconds.bind(this), { timeout: timeoutMs }).toBeGreaterThanOrEqual(seconds);
    await this.byId('voice-record-stop').click();
    await expect(this.byId('voice-preview')).toBeVisible();
  }

  /** Fills names and the consent checkbox. */
  async fillConsent(entry: VoiceProfileEntry): Promise<void> {
    await this.byId('voice-display-name').fill(entry.displayName);
    await this.byId('voice-person-name').fill(entry.personName);
    await this.byId('voice-consent-checkbox').setChecked(entry.consent);
  }

  /** Deletes a profile through the confirm dialog, optionally with its converted takes. */
  async delete(profileId: string, deleteTakes: boolean): Promise<void> {
    await this.part('delete', profileId).click();
    await this.byId(`voice-delete-takes-${profileId}`).setChecked(deleteTakes);
    await this.byId('voice-delete-confirm').click();
    await expect(this.row(profileId)).toHaveCount(0);
  }

  /** Seconds shown by the recorder timer (`m:ss`; 0 when not parsable). */
  async timerSeconds(): Promise<number> {
    const match = timerPattern.exec((await this.timer.textContent()) ?? '');
    return match === null ? 0 : Number(match[1]) * 60 + Number(match[2]);
  }
}
