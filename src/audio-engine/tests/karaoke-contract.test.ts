import { describe, expect, it } from 'vitest';
import { KARAOKE_NOT_IMPLEMENTED } from '../src/engine';
import { ENGINE_CONTRACT_VERSION, createEngine, type AudioEngine, type KaraokeLyrics, type KaraokeState } from '../src/index';
import { MockAudioContext } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';

/** Skeleton tests of the 2.1.0 karaoke contract (architect, 006 T005); audio-engine replaces the stubs in T026/T036. */
const LYRICS: KaraokeLyrics = {
  lines: [{ startMs: 1000, endMs: 2000, words: [{ text: 'Fake', startMs: 1000, endMs: 2000 }] }],
};

function engine(): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext() });
}

describe('karaoke contract 2.1.0 (skeleton)', () => {
  it('exports version 2.1.0 and the karaoke state names', () => {
    const states: readonly KaraokeState[] = ['hidden', 'countdown', 'singing', 'ended'];

    expect(ENGINE_CONTRACT_VERSION).toBe('2.1.0');
    expect(states).toHaveLength(4);
  });

  it('has the three karaoke methods, which throw until the karaoke module exists', () => {
    const subject = engine();

    expect(() => {
      subject.attachKaraoke('karaoke-banner', LYRICS, { offsetMs: 0 });
    }).toThrow(KARAOKE_NOT_IMPLEMENTED);
    expect(() => {
      subject.setKaraokeOffset(-120);
    }).toThrow(KARAOKE_NOT_IMPLEMENTED);
    expect(() => {
      subject.detachKaraoke();
    }).toThrow(KARAOKE_NOT_IMPLEMENTED);
  });

  it('rejects karaoke calls after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    expect(() => {
      subject.detachKaraoke();
    }).toThrow('Audio engine has been disposed');
  });
});
