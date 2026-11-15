/**
 * Set-up steps and label checks shared by the feature 009/010 specs.
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import type { StemStudioApi, TrackDto } from './api';
import { fakeJobTimeoutMs } from './env';
import { readVorbisComments, type VorbisComments } from './flac-tags';
import { present, readEthicsCases } from './generative';
import type { GenerationApi, GenerationDto, TakeDto, VocalLabel } from './generation-api';
import type { SongRequestDto, VoiceApi } from './voice-api';

/** Longest wait for a fake generation (`STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS` = 1, a few steps). */
export const generationTimeoutMs = 90_000;

/** Longest wait for a fake `slow` generation to finish on its own. */
export const slowGenerationTimeoutMs = 240_000;

/** The completed 20 s source track of the restyle specs (fake separation; cached across runs). */
export async function sourceTrack(api: StemStudioApi): Promise<TrackDto> {
  return api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs);
}

/** Restyles `trackId` with preset `styleName` over `start`-`end` s through the API; waits for completion. */
export async function restyledTake(generationApi: GenerationApi, trackId: string, styleName: string, range = { start: 2, end: 14 }): Promise<GenerationDto> {
  const style = await generationApi.preset(styleName);
  const body = { styleId: style.id, mode: 'range', rangeStartSeconds: range.start, rangeEndSeconds: range.end } as const;
  const queued = await generationApi.restyleOk(trackId, body);
  return generationApi.waitForCompleted(queued.id, generationTimeoutMs);
}

/** Creates a song through the API and waits for completion. */
export async function songTake(voiceApi: VoiceApi, generationApi: GenerationApi, body: SongRequestDto): Promise<GenerationDto> {
  const queued = await voiceApi.createSongOk(body);
  return generationApi.waitForCompleted(queued.id, generationTimeoutMs);
}

/** The take with `index` of a completed generation. */
export function takeOf(generation: GenerationDto, index = 1): TakeDto {
  return present(generation.takes.find((candidate) => candidate.index === index), `take ${String(index)} of ${generation.id}`);
}

/**
 * SC-005 (009) / FR-011 (010): the take is AI-labelled in the API, in the library (origin
 * `generation`) and in the file (`AI_GENERATED=true`, generator name, generation and take id;
 * `AI_VOICE` = `vocal` for song and conversion takes, absent when there is no voice).
 */
export async function expectAiLabels(generationApi: GenerationApi, take: TakeDto): Promise<void> {
  expect(take.aiGenerated).toBe(true);
  expectFileLabels(readVorbisComments(await generationApi.takeBytes(take)), take);
  const item = await generationApi.libraryItem(present(take.libraryAssetId, 'library item of the take'));
  expect([item.status, item.provenance?.origin]).toEqual([200, 'generation']);
}

function expectFileLabels(tags: VorbisComments, take: TakeDto): void {
  for (const field of readEthicsCases().aiLabels.flacTags) {
    expect(tags[field]?.[0] ?? '', `FLAC tag ${field}`).not.toBe('');
  }
  expect(tags.AI_GENERATED).toEqual(['true']);
  expect(tags.STEMSTUDIO_TAKE).toEqual([String(take.index)]);
  expectVoiceTag(tags.AI_VOICE, take.vocal);
}

function expectVoiceTag(values: readonly string[] | undefined, vocal: VocalLabel | null): void {
  if (vocal === null || vocal === 'none') {
    expect(values, 'AI_VOICE on a take without a voice').toBeUndefined();
    return;
  }
  expect(values).toEqual([vocal]);
}
