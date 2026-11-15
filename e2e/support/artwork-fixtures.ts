/**
 * Access to the feature 005 artwork fixtures (`tests/fixtures/generate_artwork_fixtures.py`) and
 * their committed manifest `tests/fixtures/artwork-manifest.json`.
 *
 * @packageDocumentation
 */
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';

/** Files written by the artwork generator. */
export type ArtworkFixtureName =
  | 'cover_embedded_10s.mp3'
  | 'cover_embedded_10s.flac'
  | 'own_picture_800x600.png'
  | 'own_picture_640x640.jpg'
  | 'not_a_picture.png'
  | 'unsupported_picture.gif';

/** Facts of one artwork fixture. */
export interface ArtworkFixtureFacts {
  readonly kind: 'audio-with-cover' | 'picture' | 'invalid-picture';
  readonly format: string;
  readonly durationSeconds: number | null;
  readonly tags: { readonly title: string; readonly artist: string; readonly album: string } | null;
  readonly picture: { readonly mediaType: string; readonly width: number; readonly height: number; readonly sha256: string } | null;
  readonly expectedArtworkSource: 'embedded' | null;
  readonly uploadStatus: number | null;
}

const fixtureRoot = path.resolve(__dirname, '..', '..', 'tests', 'fixtures');

/** Facts of one fixture from `artwork-manifest.json`. */
export function artworkFacts(name: ArtworkFixtureName): ArtworkFixtureFacts {
  const text = readFileSync(path.join(fixtureRoot, 'artwork-manifest.json'), 'utf-8');
  const manifest = JSON.parse(text) as { fixtures: Record<ArtworkFixtureName, ArtworkFixtureFacts> };
  return manifest.fixtures[name];
}

/**
 * Absolute path of a generated artwork fixture.
 * @throws Error naming the generator command when the file has not been generated yet.
 */
export function artworkFixturePath(name: ArtworkFixtureName): string {
  const file = path.join(fixtureRoot, 'audio', name);
  if (!existsSync(file)) {
    throw new Error(`Fixture ${name} is missing; run: uv run --project worker python tests/fixtures/generate_artwork_fixtures.py`);
  }
  return file;
}
