/**
 * Downloads and decodes the stems of one track.
 *
 * @packageDocumentation
 */
import type { StemName, StemSource } from './types';

/** Decoded stems of one track, keyed by stem name (insertion order = request order). */
export type StemBuffers = ReadonlyMap<StemName, AudioBuffer>;

/**
 * Fetches all stems in parallel and decodes them with `decodeAudioData`.
 *
 * @param context - Context used for decoding.
 * @param fetchFn - `fetch` implementation.
 * @param stems - Stems to load; names must be unique.
 * @returns The decoded buffers.
 * @throws Error naming the stem on an HTTP, network or decode failure.
 */
export async function loadStems(
  context: BaseAudioContext,
  fetchFn: typeof fetch,
  stems: readonly StemSource[],
): Promise<StemBuffers> {
  assertUnique(stems);
  const entries = await Promise.all(stems.map((stem) => loadStem(context, fetchFn, stem)));
  return new Map(entries);
}

/**
 * Duration of a track: the longest stem.
 *
 * @param buffers - Decoded stems.
 * @returns Duration in seconds (0 when empty).
 */
export function trackDuration(buffers: StemBuffers): number {
  return Math.max(0, ...[...buffers.values()].map((b) => b.duration));
}

function assertUnique(stems: readonly StemSource[]): void {
  const seen = new Set<StemName>();
  for (const stem of stems) {
    if (seen.has(stem.name)) {
      throw new Error(`Duplicate stem "${stem.name}"`);
    }
    seen.add(stem.name);
  }
}

async function loadStem(
  context: BaseAudioContext,
  fetchFn: typeof fetch,
  stem: StemSource,
): Promise<[StemName, AudioBuffer]> {
  const data = await download(fetchFn, stem);
  try {
    return [stem.name, await context.decodeAudioData(data)];
  } catch (error) {
    throw new Error(`Failed to decode stem "${stem.name}": ${messageOf(error)}`, { cause: error });
  }
}

async function download(fetchFn: typeof fetch, stem: StemSource): Promise<ArrayBuffer> {
  let response: Response;
  try {
    response = await fetchFn(stem.url);
  } catch (error) {
    throw new Error(`Failed to load stem "${stem.name}": ${messageOf(error)}`, { cause: error });
  }
  if (!response.ok) {
    throw new Error(`Failed to load stem "${stem.name}": HTTP ${String(response.status)}`);
  }
  return response.arrayBuffer();
}

/**
 * Readable message of an unknown thrown value.
 *
 * @param error - Caught value.
 * @returns Its message.
 */
export function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
