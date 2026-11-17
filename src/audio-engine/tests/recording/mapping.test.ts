import { describe, expect, it } from 'vitest';
import { CaptureBuffer } from '../../src/recording/capture-buffer';
import { MIN_PARTIAL_PASS_SAMPLES, mapCaptureToTakes, type CaptureMapInput, type MappedTake, type TransportSegment } from '../../src/recording/mapping';

/** Capture mapping (data-model 3.2, FR-008, FR-010): captured context frames -> song samples per pass. */
const SR = 44100;

/** A capture whose sample at context frame f is `f / 2^24` (so the source frame of every take sample is known). */
function ramp(from: number, to: number, blockFrames = 2048, channels: 1 | 2 = 1): CaptureBuffer {
  const capture = new CaptureBuffer(channels);
  for (let frame = from; frame < to; frame += blockFrames) {
    const length = Math.min(blockFrames, to - frame);
    const data = Float32Array.from({ length }, (_, i) => (frame + i) / 2 ** 24);
    capture.append(frame, channels === 1 ? [data] : [data, data.map((x) => -x)]);
  }
  return capture;
}

/** Context frame a take sample was captured at. */
function sourceFrame(take: MappedTake, index: number): number {
  return Math.round((take.channels[0]?.[index] ?? Number.NaN) * 2 ** 24);
}

function input(partial: Partial<CaptureMapInput> & Pick<CaptureMapInput, 'capture' | 'segments'>): CaptureMapInput {
  return { loop: null, compensation: 0, stopFrame: Infinity, trackSamples: 600 * SR, ...partial };
}

function open(whenFrame: number, offsetSample: number): TransportSegment {
  return { whenFrame, offsetSample, untilFrame: null };
}

function summary(takes: readonly MappedTake[]): unknown[] {
  return takes.map((t) => ({ passIndex: t.passIndex, partial: t.partial, startSample: t.startSample, lengthSamples: t.lengthSamples }));
}

describe('mapCaptureToTakes without loop', () => {
  it('maps frame f to song sample of f - C: one take from the play start to the stop', () => {
    const capture = ramp(0, 10 * SR);
    const takes = mapCaptureToTakes(input({ capture, segments: [open(SR, 0)], compensation: 441, stopFrame: 5 * SR }));
    expect(summary(takes)).toEqual([{ passIndex: null, partial: false, startSample: 0, lengthSamples: 4 * SR }]);
    const [take] = takes as [MappedTake];
    expect([sourceFrame(take, 0), sourceFrame(take, take.lengthSamples - 1)]).toEqual([SR + 441, 5 * SR + 440]);
    expect([take.firstFrame, take.dropouts, take.channels.length]).toEqual([SR + 441, 0, 1]);
  });

  it('applies the compensation: a larger C takes later frames for the same song samples', () => {
    const capture = ramp(0, 10 * SR);
    const [a] = mapCaptureToTakes(input({ capture, segments: [open(SR, 2 * SR)], compensation: 0, stopFrame: 3 * SR }));
    const [b] = mapCaptureToTakes(input({ capture, segments: [open(SR, 2 * SR)], compensation: 4410, stopFrame: 3 * SR }));
    expect([a?.startSample, b?.startSample]).toEqual([2 * SR, 2 * SR]);
    expect([sourceFrame(a as MappedTake, 0), sourceFrame(b as MappedTake, 0)]).toEqual([SR, SR + 4410]);
  });

  it('drops frames before the play start (count-in, negative song time)', () => {
    const capture = ramp(0, 6 * SR);
    const [take] = mapCaptureToTakes(input({ capture, segments: [open(3 * SR, 0)], stopFrame: 4 * SR }));
    expect([take?.startSample, take?.lengthSamples, sourceFrame(take as MappedTake, 0)]).toEqual([0, SR, 3 * SR]);
  });

  it('starts later in the song when the capture begins after the play start', () => {
    const capture = ramp(2 * SR, 6 * SR);
    const [take] = mapCaptureToTakes(input({ capture, segments: [open(SR, 0)], compensation: 100, stopFrame: 4 * SR }));
    expect([take?.startSample, take?.lengthSamples]).toEqual([SR - 100, 3 * SR - (SR - 100)]);
  });

  it('ends at the end of the track', () => {
    const capture = ramp(0, 10 * SR);
    const [take] = mapCaptureToTakes(input({ capture, segments: [open(SR, 0)], stopFrame: 9 * SR, trackSamples: 3 * SR }));
    expect([take?.startSample, take?.lengthSamples]).toEqual([0, 3 * SR]);
  });

  it('ends where the capture ends (input lost) and keeps the take, marked partial when interrupted', () => {
    const capture = ramp(0, 4 * SR);
    const [take] = mapCaptureToTakes(input({ capture, segments: [open(SR, 0)], stopFrame: 9 * SR, interrupted: true }));
    expect(summary(take === undefined ? [] : [take])).toEqual([{ passIndex: null, partial: true, startSample: 0, lengthSamples: 3 * SR }]);
  });

  it('returns no take when the recording stopped during the count-in', () => {
    const capture = ramp(0, 4 * SR);
    expect(mapCaptureToTakes(input({ capture, segments: [open(3 * SR, 0)], stopFrame: 2 * SR }))).toEqual([]);
    expect(mapCaptureToTakes(input({ capture, segments: [] }))).toEqual([]);
    expect(mapCaptureToTakes(input({ capture: new CaptureBuffer(1), segments: [open(0, 0)], stopFrame: SR }))).toEqual([]);
  });
});

describe('mapCaptureToTakes with loop passes', () => {
  const loop = { startSample: 2 * SR, endSample: 4 * SR };

  it('cuts one take per pass to [loopStart, loopEnd) with 1-based pass numbers', () => {
    const capture = ramp(0, 12 * SR);
    const takes = mapCaptureToTakes(input({ capture, segments: [open(SR, 2 * SR)], loop, compensation: 441, stopFrame: SR + 4 * SR + 1.5 * SR }));
    expect(summary(takes)).toEqual([
      { passIndex: 1, partial: false, startSample: 2 * SR, lengthSamples: 2 * SR },
      { passIndex: 2, partial: false, startSample: 2 * SR, lengthSamples: 2 * SR },
      { passIndex: 3, partial: true, startSample: 2 * SR, lengthSamples: 1.5 * SR },
    ]);
    expect(takes.map((t) => sourceFrame(t, 0))).toEqual([SR + 441, 3 * SR + 441, 5 * SR + 441]);
  });

  it('keeps a partial pass of at least 1 s and drops a shorter one', () => {
    const capture = ramp(0, 12 * SR);
    expect(MIN_PARTIAL_PASS_SAMPLES).toBe(SR);
    const keep = mapCaptureToTakes(input({ capture, segments: [open(SR, 2 * SR)], loop, stopFrame: 3 * SR + SR }));
    const drop = mapCaptureToTakes(input({ capture, segments: [open(SR, 2 * SR)], loop, stopFrame: 3 * SR + SR - 1 }));
    expect([keep.length, keep[1]?.lengthSamples, drop.length]).toEqual([2, SR, 1]);
  });

  it('marks a first pass started inside the loop as partial (it starts at the position)', () => {
    const capture = ramp(0, 12 * SR);
    const takes = mapCaptureToTakes(input({ capture, segments: [open(SR, 2.5 * SR)], loop, stopFrame: SR + 1.5 * SR + 2 * SR }));
    expect(summary(takes)).toEqual([
      { passIndex: 1, partial: true, startSample: 2.5 * SR, lengthSamples: 1.5 * SR },
      { passIndex: 2, partial: false, startSample: 2 * SR, lengthSamples: 2 * SR },
    ]);
  });

  it('drops the frames in count-in gaps between explicit passes', () => {
    const capture = ramp(0, 12 * SR);
    const gap = SR;
    const segments: TransportSegment[] = [
      { whenFrame: SR, offsetSample: 2 * SR, untilFrame: 3 * SR },
      { whenFrame: 3 * SR + gap, offsetSample: 2 * SR, untilFrame: 5 * SR + gap },
      { whenFrame: 5 * SR + 2 * gap, offsetSample: 2 * SR, untilFrame: 7 * SR + 2 * gap },
    ];
    const takes = mapCaptureToTakes(input({ capture, segments, loop, stopFrame: 10 * SR }));
    expect(summary(takes)).toEqual([1, 2, 3].map((passIndex) => ({ passIndex, partial: false, startSample: 2 * SR, lengthSamples: 2 * SR })));
    expect(takes.map((t) => sourceFrame(t, 0))).toEqual([SR, 4 * SR, 7 * SR]);
  });

  it('numbers passes by the transport even when the capture misses one (no take for it)', () => {
    const capture = ramp(3 * SR, 12 * SR);
    const takes = mapCaptureToTakes(input({ capture, segments: [open(SR, 2 * SR)], loop, stopFrame: 7 * SR }));
    expect(takes.map((t) => t.passIndex)).toEqual([2, 3]);
  });
});

describe('frame gaps', () => {
  it('fills a gap in the frame stamps with zeros and counts it as a dropout of the take it falls in', () => {
    const capture = new CaptureBuffer(2);
    const block = (frame: number, length: number): Float32Array[] => [new Float32Array(length).fill(0.5), new Float32Array(length).fill(-0.5)];
    capture.append(0, block(0, SR));
    capture.append(SR + 100, block(SR + 100, SR));
    capture.append(2 * SR + 300, block(2 * SR + 300, SR));
    expect([capture.firstFrame, capture.endFrame, capture.gaps]).toEqual([0, 3 * SR + 300, 2]);
    const [take] = mapCaptureToTakes(input({ capture, segments: [open(0, 0)], stopFrame: 3 * SR }));
    expect([take?.dropouts, take?.channels.length]).toEqual([2, 2]);
    expect([take?.channels[0]?.[SR - 1], take?.channels[0]?.[SR], take?.channels[0]?.[SR + 99], take?.channels[1]?.[SR + 100]]).toEqual([0.5, 0, 0, -0.5]);
  });

  it('counts only the gaps inside a take', () => {
    const capture = ramp(0, SR);
    capture.append(2 * SR, [new Float32Array(SR)]);
    const [take] = mapCaptureToTakes(input({ capture, segments: [open(0, 0)], stopFrame: SR / 2 }));
    expect(take?.dropouts).toBe(0);
  });

  it('ignores frames that overlap already captured ones', () => {
    const capture = ramp(0, 4096);
    capture.append(4000, [new Float32Array(200).fill(1)]);
    expect(capture.endFrame).toBe(4200);
    const [a, b] = capture.read(4095, 2)[0] ?? [];
    expect([Math.round((a ?? 0) * 2 ** 24), b]).toEqual([4095, 1]);
    capture.append(100, [new Float32Array(10)]);
    expect(capture.endFrame).toBe(4200);
  });

  it('reads zeros outside the captured range', () => {
    const capture = ramp(10, 20);
    const [read] = capture.read(5, 20);
    expect([read?.[4], Math.round((read?.[5] ?? 0) * 2 ** 24), read?.[15]]).toEqual([0, 10, 0]);
  });
});
