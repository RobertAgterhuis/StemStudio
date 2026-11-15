import type { Locator, Page } from '@playwright/test';
import { setBlazorInputFiles } from '../support/input-file';

/** `<artworkSource>` of ui-test-ids 1.4.0 (`none` = placeholder). */
export type ShownArtworkSource = 'user' | 'spotify' | 'embedded' | 'coverartarchive' | 'youtube' | 'none';

/**
 * Artwork and metadata ids of feature 005 on the track list, the track detail page and the mixer
 * (ui-test-ids 1.4.0). Kept as its own object (fixture `artwork`) rather than members of
 * `TrackListPage`, `TrackDetailPage` and `MixerPage`, because the same ids appear on all three.
 */
export class ArtworkView {
  /** @param page The Playwright page. */
  constructor(private readonly page: Page) {}

  /** Thumbnail (or placeholder) of a track row on `/tracks`. */
  thumb(trackId: string): Locator {
    return this.page.getByTestId(`track-thumb-${trackId}`);
  }

  /** Cover (or placeholder) on the track detail page. */
  get trackCover(): Locator {
    return this.page.getByTestId('track-cover');
  }

  /** Cover in the mixer header. */
  get mixerCover(): Locator {
    return this.page.getByTestId('mixer-cover');
  }

  /** "<album> (<year>)" in the mixer header (status `found` only). */
  get mixerAlbum(): Locator {
    return this.page.getByTestId('mixer-album');
  }

  /** Lookup status on the track detail page (`data-status`). */
  get metadataStatus(): Locator {
    return this.page.getByTestId('metadata-status');
  }

  /** File input for an own picture (Advanced/Expert). */
  get uploadInput(): Locator {
    return this.page.getByTestId('artwork-upload-input');
  }

  /** "Use automatic picture" (only while the own picture is shown). */
  get reset(): Locator {
    return this.page.getByTestId('artwork-reset');
  }

  /** Readable upload error. */
  get error(): Locator {
    return this.page.getByTestId('artwork-error');
  }

  /** Selects an own picture; the upload starts on selection. */
  async uploadPicture(file: string): Promise<void> {
    await setBlazorInputFiles(this.uploadInput, file);
  }
}
