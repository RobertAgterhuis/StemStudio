import { describe, expect, it, vi } from 'vitest';
import { FrameLoop } from '../../src/frame-loop';
import { TimelineFrames } from '../../src/timeline/frames';
import { FakeFrames } from '../mocks/frames';

function rig(playing = false): { frames: FakeFrames; draw: ReturnType<typeof vi.fn>; subject: TimelineFrames; state: { playing: boolean } } {
  const frames = new FakeFrames();
  const loop = new FrameLoop({ frames, document });
  const draw = vi.fn();
  const state = { playing };
  const subject = new TimelineFrames({ loop, playing: () => state.playing, draw });
  return { frames, draw, subject, state };
}

describe('TimelineFrames (one-frame redraw triggers)', () => {
  it('draws nothing until started', () => {
    const { frames, draw, subject } = rig();
    subject.redraw();
    subject.onReposition();
    subject.onTimingChanged();
    subject.onStateChanged('playing');
    expect(frames.size).toBe(0);
    expect(subject.active).toBe(false);
    expect(draw).not.toHaveBeenCalled();
  });

  it('draws one frame after start while stopped and one after every trigger', () => {
    const { frames, draw, subject } = rig();
    subject.start();
    frames.step(1);
    expect(draw).toHaveBeenCalledTimes(1);
    expect(frames.size).toBe(0);
    for (const trigger of [() => { subject.redraw(); }, () => { subject.onReposition(); }, () => { subject.onTimingChanged(); }, () => { subject.onStateChanged('paused'); }]) {
      trigger();
      frames.step(2);
    }
    expect(draw).toHaveBeenCalledTimes(5);
    expect(frames.size).toBe(0);
  });

  it('draws every frame while playing and stops following on pause', () => {
    const { frames, draw, subject, state } = rig(true);
    subject.start();
    frames.step(1);
    frames.step(2);
    frames.step(3);
    expect(draw).toHaveBeenCalledTimes(3);
    state.playing = false;
    subject.onStateChanged('paused');
    frames.step(4);
    expect(frames.size).toBe(0);
    subject.onStateChanged('playing');
    frames.step(5);
    expect(frames.size).toBe(1);
  });

  it('stop() ends the drawing and later triggers are ignored', () => {
    const { frames, draw, subject } = rig(true);
    subject.start();
    frames.step(1);
    subject.stop();
    frames.step(2);
    subject.redraw();
    expect(frames.size).toBe(0);
    expect(draw).toHaveBeenCalledTimes(1);
    expect(subject.active).toBe(false);
  });
});
