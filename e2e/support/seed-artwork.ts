/**
 * Seeds the stack with tracks that have a picture (feature 005, quickstart A9, SC-003): unique
 * copies of a WAV fixture are imported and each gets an own picture, so `/tracks` shows at least
 * `count` thumbnails. Idempotent: the copies are deterministic per index, so a rerun gets the
 * cached tracks back (200) instead of new ones, and tracks found by title prefix are reused.
 * The `@perf` case of artwork.spec.ts calls it.
 *
 * @packageDocumentation
 */
import type { StemStudioApi } from './api';
import { artworkFixturePath } from './artwork-fixtures';
import { fakeJobTimeoutMs } from './env';
import { fixturePath } from './fixtures';
import type { MetadataApi } from './metadata-api';
import { uniqueWavCopy } from './unique-audio';

/** Title prefix of seeded tracks (the upload file name without extension becomes the title). */
export const seedPrefix = 'click_120bpm_4-4_12s-seed-artwork-';

/** Clients the seeding needs. */
export interface SeedClients {
  readonly api: StemStudioApi;
  readonly metadataApi: MetadataApi;
}

/** Ids of the seeded tracks (existing ones first), at least `count`. */
export async function seedArtworkTracks(clients: SeedClients, count: number): Promise<string[]> {
  const existing = (await clients.api.listTracks()).filter((t) => t.title.startsWith(seedPrefix)).map((t) => t.id);
  const ids = [...existing];
  for (let n = existing.length; n < count; n++) {
    ids.push(await seedOne(clients, n));
  }
  return ids;
}

async function seedOne(clients: SeedClients, n: number): Promise<string> {
  const copy = uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `seed-artwork-${String(n).padStart(3, '0')}`);
  const track = await clients.api.ensureFileCompleted(copy, fakeJobTimeoutMs);
  const picture = n % 2 === 0 ? 'own_picture_800x600.png' : 'own_picture_640x640.jpg';
  await clients.metadataApi.setArtwork(track.id, artworkFixturePath(picture));
  return track.id;
}
