import { afterEach, describe, expect, it, vi } from 'vitest';
import { FrameLoop } from '../src/frame-loop';
import { Meters } from '../src/meters';
import { MockAudioContext } from './mocks/audio-context';
import { FakeFrames, setVisibility } from './mocks/frames';

interface Rig {
  readonly frames: FakeFrames;
  readonly doc: Document;
  readonly loop: FrameLoop;
}

function makeRig(): Rig {
  const frames = new FakeFrames();
  const doc = document.implementation.createHTMLDocument('frame-loop');
  setVisibility(doc, 'visible');
  return { frames, doc, loop: new FrameLoop({ frames, document: doc }) };
}

describe('FrameLoop (shared requestAnimationFrame loop)', () => {
  it('starts with the first subscriber, serves all subscribers from one frame and stops with the last', () => {
    const { frames, loop } = makeRig();
    const a = vi.fn();
    const b = vi.fn();
    expect(frames.size).toBe(0);
    loop.add(a);
    loop.add(b);
    loop.add(a);
    expect(frames.size).toBe(1);
    expect(loop.running).toBe(true);
    frames.step(16);
    expect(a).toHaveBeenCalledExactlyOnceWith(16);
    expect(b).toHaveBeenCalledExactlyOnceWith(16);
    expect(frames.size).toBe(1);
    loop.remove(a);
    expect(frames.size).toBe(1);
    loop.remove(b);
    expect(frames.size).toBe(0);
    expect(loop.running).toBe(false);
    loop.remove(b);
    expect(frames.size).toBe(0);
  });

  it('once() draws exactly one frame and does not keep the loop running', () => {
    const { frames, loop } = makeRig();
    const draw = vi.fn();
    loop.once(draw);
    loop.once(draw);
    expect(frames.size).toBe(1);
    frames.step(16);
    expect(draw).toHaveBeenCalledOnce();
    expect(frames.size).toBe(0);
  });

  it('a callback that is both continuous and requested once runs once per frame', () => {
    const { frames, loop } = makeRig();
    const draw = vi.fn();
    loop.add(draw);
    loop.once(draw);
    frames.step(16);
    expect(draw).toHaveBeenCalledOnce();
    expect(frames.size).toBe(1);
  });

  it('a once() request made inside a frame is served by the next frame', () => {
    const { frames, loop } = makeRig();
    const later = vi.fn();
    loop.once(() => {
      loop.once(later);
    });
    frames.step(16);
    expect(later).not.toHaveBeenCalled();
    expect(frames.size).toBe(1);
    frames.step(32);
    expect(later).toHaveBeenCalledExactlyOnceWith(32);
  });

  it('removing the last subscriber inside a frame does not request another frame', () => {
    const { frames, loop } = makeRig();
    const self = (): void => {
      loop.remove(self);
    };
    loop.add(self);
    frames.step(16);
    expect(frames.size).toBe(0);
  });

  it('pauses while the document is hidden and resumes, including pending one-shot frames, when visible', () => {
    const { frames, loop, doc } = makeRig();
    const draw = vi.fn();
    const once = vi.fn();
    loop.add(draw);
    setVisibility(doc, 'hidden');
    expect(frames.size).toBe(0);
    expect(loop.running).toBe(false);
    loop.once(once);
    loop.add(vi.fn());
    expect(frames.size).toBe(0);
    setVisibility(doc, 'visible');
    expect(frames.size).toBe(1);
    frames.step(48);
    expect(draw).toHaveBeenCalledExactlyOnceWith(48);
    expect(once).toHaveBeenCalledOnce();
  });

  it('becoming visible without subscribers requests nothing, and repeated visible events request one frame', () => {
    const { frames, loop, doc } = makeRig();
    setVisibility(doc, 'visible');
    expect(frames.size).toBe(0);
    loop.add(vi.fn());
    setVisibility(doc, 'visible');
    expect(frames.size).toBe(1);
  });

  it('dispose cancels the frame, forgets subscribers and stops following visibility', () => {
    const { frames, loop, doc } = makeRig();
    const draw = vi.fn();
    loop.add(draw);
    loop.once(draw);
    loop.dispose();
    expect(frames.size).toBe(0);
    setVisibility(doc, 'hidden');
    setVisibility(doc, 'visible');
    expect(frames.size).toBe(0);
    frames.step(16);
    expect(draw).not.toHaveBeenCalled();
  });

  it('meters and a second subscriber share one animation frame', () => {
    const { frames, loop } = makeRig();
    const analyser = new MockAudioContext().createAnalyser();
    analyser.samples = [0.5];
    const element = document.createElement('div');
    element.id = 'meter-master';
    document.body.replaceChildren(element);
    const meters = new Meters({ source: () => analyser as unknown as AnalyserNode, loop, document });
    const karaoke = vi.fn();
    meters.attach('master', 'meter-master');
    loop.add(karaoke);
    expect(frames.size).toBe(1);
    frames.step(16);
    expect(element.dataset.peakDb).toBe('-6');
    expect(karaoke).toHaveBeenCalledOnce();
    meters.detach('master');
    expect(frames.size).toBe(1);
    loop.remove(karaoke);
    expect(frames.size).toBe(0);
  });
});

describe('FrameLoop defaults', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('uses requestAnimationFrame and the global document by default', () => {
    const callbacks: FrameRequestCallback[] = [];
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => callbacks.push(cb));
    const cancel = vi.fn();
    vi.stubGlobal('cancelAnimationFrame', cancel);
    const loop = new FrameLoop();
    const draw = vi.fn();
    loop.add(draw);
    callbacks[0]?.(16);
    expect(draw).toHaveBeenCalledWith(16);
    loop.dispose();
    expect(cancel).toHaveBeenCalledWith(2);
  });
});
