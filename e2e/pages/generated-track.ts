import { expect, type Locator } from '@playwright/test';
import { TestIdPage } from './test-id-page';

/**
 * Generation ids on the existing track pages (ui-test-ids 1.8.0 `track-origin-*`,
 * `track-ai-badge-*`, `track-restyle`; 1.9.0 `track-lyrics-link`, `track-vocal-badge`). Kept apart
 * from `track-detail-page.ts` / `track-list-page.ts` until those can be edited (qa 009/010 pre-UI
 * hand-off).
 */
export class GeneratedTrack extends TestIdPage {
  /** "AI-generated" text badge on the track page. */
  get originBadge(): Locator {
    return this.byId('track-origin-badge');
  }

  /** Link to the source track of a restyled take. */
  get sourceLink(): Locator {
    return this.byId('track-origin-source-link');
  }

  /** Link to the lyrics sheet of a generated song. */
  get lyricsLink(): Locator {
    return this.byId('track-lyrics-link');
  }

  /** "AI voice" / "AI voice: <name>" on the track page. */
  get vocalBadge(): Locator {
    return this.byId('track-vocal-badge');
  }

  /** The track page section with the track's generations. */
  get generations(): Locator {
    return this.byId('track-generations');
  }

  /** "AI" badge of a row in `/tracks`. */
  listBadge(trackId: string): Locator {
    return this.byId(`track-ai-badge-${trackId}`);
  }

  /** Opens `/tracks/{id}` and waits for the title. */
  async goto(trackId: string): Promise<void> {
    await this.page.goto(`/tracks/${trackId}`);
    await expect(this.byId('track-title')).toBeVisible();
  }
}
