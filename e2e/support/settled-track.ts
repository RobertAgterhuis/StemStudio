/**
 * A completed fixture track whose band-info lookup has settled (flake list, `docs/process/FLAKES.md`).
 *
 * A new track's lookup starts when the track completes and, under suite load, waits in the worker's metadata lane (one
 * lookup at a time). When it finishes it re-files every item of the track (feature 022, `MetadataJobApplier`): an item
 * saved while the lookup was still `searching` moves from "Unknown artist > Unknown album" into "The Fixture Band >
 * Synthetic Sessions", and the save confirmation's `export-saved-open` / `score-saved-open` link (which names the song
 * folder at save time) then opens a folder without the row. Specs that follow such a link start from this helper instead of `ensureCompleted`.
 *
 * @packageDocumentation
 */
import type { DetailFixtureName, FixtureName } from './fixtures';
import type { StemStudioApi, TrackDto } from './api';
import type { MetadataApi } from './metadata-api';

/**
 * Uploads `name` (or reuses its track), waits until it is completed and until its band-info lookup is settled.
 *
 * @param api The StemStudio API client.
 * @param metadataApi The metadata client.
 * @param name The fixture.
 * @param timeoutMs Budget of each wait (the lookup may queue behind worker jobs, so the fake job budget fits).
 * @returns The completed track (re-read, so its facts include the lookup result).
 */
export async function ensureSettledTrack(
  api: StemStudioApi,
  metadataApi: MetadataApi,
  name: FixtureName | DetailFixtureName,
  timeoutMs: number,
): Promise<TrackDto> {
  const track = await api.ensureCompleted(name, timeoutMs);
  await metadataApi.waitForMetadata(track.id, timeoutMs);
  return api.getTrack(track.id);
}
