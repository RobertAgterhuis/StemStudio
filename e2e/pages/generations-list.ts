import { expect, type Locator } from '@playwright/test';
import type { GenerationState, VocalLabel } from '../support/generation-api';
import { TestIdPage } from './test-id-page';

/** A take element of ui-test-ids 1.8.0/1.9.0 (`take-<part>-<generationId>-<index>`). */
export type TakePart = 'play' | 'import' | 'details' | 'ai-badge' | 'vocal-badge' | 'vocal-play' | 'use-my-voice';

/**
 * Generation rows and take actions (ui-test-ids 1.8.0 `generation-*`, `take-*`; 1.9.0 vocal ids).
 * The same ids are used on `/generations`, in the track page section `track-generations` and in
 * the song page list `song-generations`.
 */
export class GenerationsList extends TestIdPage {
  /** Opens `/generations`. */
  async goto(): Promise<void> {
    await this.page.goto('/generations');
    await expect(this.byId('nav-generations')).toBeVisible();
  }

  /** The row of a generation (`data-status`, `data-kind`). */
  row(generationId: string): Locator {
    return this.byId(`generation-row-${generationId}`);
  }

  /** `generation-<part>-<id>`: `progress` (`aria-valuenow`), `stage`, `error`, `cancel`, `retry`. */
  part(part: 'progress' | 'stage' | 'error' | 'cancel' | 'retry', generationId: string): Locator {
    return this.byId(`generation-${part}-${generationId}`);
  }

  /** A take element. */
  take(part: TakePart, generationId: string, index = 1): Locator {
    return this.byId(`take-${part}-${generationId}-${String(index)}`);
  }

  /** Waits until the row shows `status`. */
  async waitForStatus(generationId: string, status: GenerationState, timeoutMs = 60_000): Promise<void> {
    await expect(this.row(generationId)).toHaveAttribute('data-status', status, { timeout: timeoutMs });
  }

  /** Deletes a terminal generation through its confirm dialog. */
  async delete(generationId: string): Promise<void> {
    await this.byId(`generation-delete-${generationId}`).click();
    await this.byId('generation-delete-confirm').click();
    await expect(this.row(generationId)).toHaveCount(0);
  }

  /** Opens the take details disclosure and returns it. */
  async openDetails(generationId: string, index = 1): Promise<Locator> {
    const details = this.take('details', generationId, index);
    await details.click();
    return details;
  }

  /** Asserts the vocal badge of a take (`data-vocal`). */
  async expectVocal(generationId: string, vocal: VocalLabel, index = 1): Promise<void> {
    await expect(this.take('vocal-badge', generationId, index)).toHaveAttribute('data-vocal', vocal);
  }
}
