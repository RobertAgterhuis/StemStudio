import { describe, expect, it } from 'vitest';
import {
  ENGINE_CONTRACT_VERSION,
  createEngine,
  type AudioEngine,
  type DotNetCallbacks,
  type LoopPassInfo,
  type ScoreFollowOptions,
  type ScoreViewMap,
} from '../src/index';
import { MockAudioContext } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';

/** Contract tests of the 2.4.0 surface (architect skeleton 014 T004; follower in T081, OnLoopPass in T043). */
const VIEW: ScoreViewMap = {
  pages: [{ page: 1, url: '/api/assets/a/score-view/pages/1?v=1', widthPx: 840, heightPx: 1188 }],
  bars: [
    { index: 0, number: 1, page: 1, x: 0.1, y: 0.1, width: 0.2, height: 0.05, startSeconds: 0, endSeconds: 2 },
    { index: 1, number: 2, page: 1, x: 0.3, y: 0.1, width: 0.2, height: 0.05, startSeconds: 2, endSeconds: 4 },
  ],
};
const FOLLOW: ScoreFollowOptions = { follow: true, reducedMotion: false, scrollPauseMs: 4000 };

function engine(): AudioEngine {
  const ctx = new MockAudioContext();
  return createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext() });
}

describe('practice flow contract 2.4.0 (skeleton)', () => {
  it('exports version 2.5.0 (contains the 2.4.0 surface) and the OnLoopPass callback', () => {
    const callback: DotNetCallbacks = 'OnLoopPass';
    const pass: LoopPassInfo = { start: 8, end: 24, pass: 1, speedPercent: 75 };

    expect(ENGINE_CONTRACT_VERSION).toBe('2.6.0');
    expect([callback, pass.pass]).toEqual(['OnLoopPass', 1]);
  });

  it('has the three score follower methods (implemented in T081, tests in score-follow.test.ts)', () => {
    const subject = engine();

    expect(() => {
      subject.attachScoreFollower('score-view', VIEW, FOLLOW);
    }).toThrow('Score host not found: score-view');
    expect(() => {
      subject.setScoreFollowOptions({ follow: false });
      subject.detachScoreFollower();
    }).not.toThrow();
  });

  it('rejects score follower calls after dispose like every other call', async () => {
    const subject = engine();
    await subject.dispose();

    expect(() => {
      subject.detachScoreFollower();
    }).toThrow(/disposed/);
  });
});
