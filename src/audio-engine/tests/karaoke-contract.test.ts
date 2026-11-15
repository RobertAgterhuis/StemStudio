import { describe, expect, it } from 'vitest';
import { ENGINE_CONTRACT_VERSION, createEngine, type AudioEngine, type KaraokeLyrics, type KaraokeState } from '../src/index';
import { MockAudioContext } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';

/** Contract tests of the 2.1.0 karaoke surface (architect skeleton 006 T005, stubs replaced in T036). */
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

  it('has the three karaoke methods; detach without an attached karaoke is a no-op', () => {
    const subject = engine();

    expect(typeof subject.attachKaraoke).toBe('function');
    expect(() => {
      subject.setKaraokeOffset(-120);
    }).not.toThrow();
    expect(() => {
      subject.detachKaraoke();
    }).not.toThrow();
    expect(() => {
      subject.attachKaraoke('karaoke-banner', LYRICS, { offsetMs: 0 });
    }).toThrow('Karaoke host not found: karaoke-banner');
  });

  it('rejects karaoke calls after dispose like every other method', async () => {
    const subject = engine();
    await subject.dispose();

    expect(() => {
      subject.detachKaraoke();
    }).toThrow('Audio engine has been disposed');
  });
});
