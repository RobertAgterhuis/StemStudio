import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/** The "Use my voice" dialog on a song take (ui-test-ids 1.9.0 `take-voice-*`). */
export class UseMyVoiceDialog extends TestIdPage {
  /** Profile select (a link to `/voices` when there is none). */
  get profileSelect(): Locator {
    return this.byId('take-voice-profile-select');
  }

  /** Opens the dialog from the "Use my voice" button of a take. */
  async open(generationId: string, index: number): Promise<void> {
    await this.byId(`take-use-my-voice-${generationId}-${String(index)}`).click();
    await expect(this.profileSelect).toBeVisible();
  }

  /** Opens the dialog from a take, picks a profile by name and starts the conversion. */
  async convert(generationId: string, index: number, profileName: string): Promise<void> {
    await this.open(generationId, index);
    await this.profileSelect.selectOption({ label: profileName });
    await this.byId('take-voice-convert-start').click();
    await expect(this.profileSelect).toBeHidden();
  }
}
