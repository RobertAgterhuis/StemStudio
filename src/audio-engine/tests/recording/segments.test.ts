import { describe, expect, it } from 'vitest';
import { MAX_LOGGED_SEGMENTS, SegmentLog } from '../../src/recording/segments';
import { makeTransport } from '../mocks/transport-rig';

/** Internal transport-segment log (015 T015, Phase 0 P0-8): heard anchors kept for the whole session. */
const SR = 44100;

describe('SegmentLog', () => {
  it('logs the heard anchor of every start, in frames, while attached', async () => {
    const { ctx, transport } = makeTransport();
    const log = new SegmentLog(SR);
    transport.setAnchorSink(log.record);
    transport.seek(2);
    await transport.play();
    expect(log.segments()).toEqual([{ whenFrame: Math.round(0.1 * SR), offsetSample: 2 * SR, untilFrame: null }]);
    ctx.currentTime = 1;
    transport.pause();
    await transport.play();
    expect(log.segments().map((s) => s.whenFrame)).toEqual([Math.round(0.1 * SR), Math.round(1.1 * SR)]);
  });

  it('keeps anchors the transport prunes (live changes) and explicit-pass ends', () => {
    const log = new SegmentLog(SR);
    log.record({ when: 1, offset: 2, until: 3 });
    log.record({ when: 3.5, offset: 2, until: 5.5 });
    expect(log.segments()).toEqual([
      { whenFrame: SR, offsetSample: 2 * SR, untilFrame: 3 * SR },
      { whenFrame: 3.5 * SR, offsetSample: 2 * SR, untilFrame: 5.5 * SR },
    ]);
  });

  it('includes the tempo-stage latency P implicitly: the heard when is P after the source start', async () => {
    const { ctx, transport } = makeTransport();
    const log = new SegmentLog(SR);
    transport.setAnchorSink(log.record);
    transport.setTiming({ rate: 1, latency: 0.05 });
    await transport.play();
    const source = ctx.sources.at(-1);
    expect(source?.startCalls[0]?.when).toBeCloseTo(0.1, 9);
    expect(log.segments()[0]?.whenFrame).toBe(Math.round(0.15 * SR));
  });

  it('logs live rate anchors as well', async () => {
    const { ctx, transport } = makeTransport();
    const log = new SegmentLog(SR);
    transport.setAnchorSink(log.record);
    await transport.play();
    ctx.currentTime = 1;
    transport.retime({ at: 1, rate: 1, latency: 0, anchor: { when: 1.2, offset: 1.1 } });
    expect(log.segments().map((s) => s.whenFrame)).toEqual([Math.round(0.1 * SR), Math.round(1.2 * SR)]);
  });

  it('stops logging when detached and forgets on clear', async () => {
    const { transport } = makeTransport();
    const log = new SegmentLog(SR);
    transport.setAnchorSink(log.record);
    await transport.play();
    transport.setAnchorSink(null);
    transport.stop();
    await transport.play();
    expect(log.segments()).toHaveLength(1);
    log.clear();
    expect(log.segments()).toEqual([]);
  });

  it(`keeps at most ${String(MAX_LOGGED_SEGMENTS)} segments (50 passes + 1), the newest`, () => {
    const log = new SegmentLog(SR);
    expect(MAX_LOGGED_SEGMENTS).toBe(51);
    for (let i = 0; i < 60; i++) {
      log.record({ when: i, offset: 0 });
    }
    const segments = log.segments();
    expect([segments.length, segments[0]?.whenFrame]).toEqual([51, 9 * SR]);
  });
});
