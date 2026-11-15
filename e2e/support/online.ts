/**
 * Online-lookup modes of the stack under test (feature 005, data-model.md 1) and the expectations
 * of the fake online provider (`tests/fixtures/metadata/fake-provider.json`).
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';

/** `STEMSTUDIO_FAKE_ONLINE` of a fake stack. */
export type FakeOnlineMode = 'found' | 'not-found' | 'failed' | 'off';

/** `Track.metadata.status` / `BandInfo.status` (backend-api 1.4.0 `MetadataStatus`). */
export type MetadataStatus = 'none' | 'pending' | 'searching' | 'found' | 'notFound' | 'failed' | 'disabled';

const fakeOnlineModes: readonly FakeOnlineMode[] = ['found', 'not-found', 'failed', 'off'];

/**
 * Parses a fake online mode; unset or empty means `found` (the worker default).
 * @throws Error naming the allowed values when `value` is none of them.
 */
export function parseFakeOnlineMode(value: string | undefined): FakeOnlineMode {
  const mode = value === undefined || value === '' ? 'found' : value;
  const known = fakeOnlineModes.find((candidate) => candidate === mode);
  if (known === undefined) {
    throw new Error(`fake online mode "${mode}" is not one of ${fakeOnlineModes.join(', ')}`);
  }
  return known;
}

/**
 * Mode of the stack under test: `E2E_FAKE_ONLINE`, else `STEMSTUDIO_FAKE_ONLINE` (the variable
 * that started the stack), else `found`. Specs that assert one mode skip in the others.
 */
export const fakeOnlineMode: FakeOnlineMode = parseFakeOnlineMode(
  process.env.E2E_FAKE_ONLINE ?? process.env.STEMSTUDIO_FAKE_ONLINE,
);

/**
 * True when the suite may use the internet (`E2E_ONLINE=1`): only then the `@online` cases
 * import real YouTube and Spotify links. The default fake-stack run never touches the network.
 */
export const onlineAllowed: boolean = process.env.E2E_ONLINE === '1';

/** Facts of a Wikipedia excerpt as the fake provider returns them. */
export interface FakeSummary {
  readonly language: string;
  readonly title: string;
  readonly extract: string;
  readonly pageUrl: string;
  readonly historyUrl: string;
  readonly licence: string;
  readonly licenceUrl: string;
}

/** The artist the fake provider returns in mode `found`. */
export interface FakeArtist {
  readonly name: string;
  readonly type: string;
  readonly country: string;
  readonly beginYear: number;
  readonly endYear: number | null;
  readonly genres: readonly string[];
  readonly members: readonly { readonly name: string; readonly fromYear: number | null; readonly toYear: number | null }[];
  readonly website: string;
  readonly summary: FakeSummary;
}

/** One mode row of the behaviour table. */
export interface FakeModeRow {
  readonly status: MetadataStatus;
  readonly artworkSource: string | null;
  readonly error?: string;
}

/** Shape of `fake-provider.json`. */
export interface FakeProvider {
  readonly modes: Readonly<Record<FakeOnlineMode, FakeModeRow>>;
  readonly found: {
    readonly recording: { readonly album: string; readonly year: number };
    readonly mixerAlbumText: string;
    readonly artist: FakeArtist;
    readonly sources: readonly string[];
  };
}

const providerFile = path.resolve(__dirname, '..', '..', 'tests', 'fixtures', 'metadata', 'fake-provider.json');

/** Reads the committed behaviour table of the fake online provider. */
export function readFakeProvider(): FakeProvider {
  return JSON.parse(readFileSync(providerFile, 'utf-8')) as FakeProvider;
}

/** Why a spec is skipped when the stack runs another mode (shown in the report). */
export function modeSkipReason(wanted: readonly FakeOnlineMode[]): string {
  return `needs STEMSTUDIO_FAKE_ONLINE=${wanted.join('|')}; the stack runs "${fakeOnlineMode}"`;
}
