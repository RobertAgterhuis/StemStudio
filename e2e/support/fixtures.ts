/**
 * Access to the generated audio fixtures and their committed manifests (`tests/fixtures/`).
 *
 * @packageDocumentation
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

/** Files of the fixture catalogue (docs/testing/TEST-STRATEGY.md section 7). */
export type FixtureName =
  | 'click_120bpm_4-4_12s.wav'
  | 'click_90bpm_3-4_12s.wav'
  | 'music_100bpm_20s.mp3'
  | 'music_100bpm_20s.flac'
  | 'upload_4mb.wav'
  | 'silence_5s.wav'
  | 'corrupt.mp3'
  | 'unsupported.ogg';

/** Detail fixtures the E2E suite imports (feature 002, `detail-manifest.json`). */
export type DetailFixtureName = 'detail_10s_band.wav';

/** Known facts of one fixture, as written by `generate_fixtures.py`. */
export interface FixtureFacts {
  readonly kind: 'click' | 'music' | 'silence' | 'corrupt' | 'unsupported';
  readonly format: string;
  readonly durationSeconds: number | null;
  readonly sampleRate: number | null;
  readonly channels: number | null;
  readonly bpm: number | null;
  readonly beatsPerBar: number | null;
  readonly beatTimes: readonly number[];
  readonly downbeatTimes: readonly number[];
}

/** Shape of `tests/fixtures/manifest.json`. */
export interface FixtureManifest {
  readonly version: number;
  readonly sampleRate: number;
  readonly fixtures: Readonly<Record<FixtureName, FixtureFacts>>;
}

const fixtureRoot = path.resolve(__dirname, '..', '..', 'tests', 'fixtures');

/** Reads the committed manifest. */
export function readManifest(): FixtureManifest {
  const text = readFileSync(path.join(fixtureRoot, 'manifest.json'), 'utf-8');
  return JSON.parse(text) as FixtureManifest;
}

/** Facts of one fixture from the manifest. */
export function fixtureFacts(name: FixtureName): FixtureFacts {
  return readManifest().fixtures[name];
}

/**
 * Absolute path of a generated fixture file.
 * @throws Error naming the generator command when the audio has not been generated yet.
 */
export function fixturePath(name: FixtureName | DetailFixtureName): string {
  const file = path.join(fixtureRoot, 'audio', name);
  if (!existsSync(file)) {
    const generator = name.startsWith('detail_') ? 'generate_detail_fixtures.py' : 'generate_fixtures.py';
    throw new Error(`Fixture ${name} is missing; run: uv run --project worker python tests/fixtures/${generator}`);
  }
  return file;
}
