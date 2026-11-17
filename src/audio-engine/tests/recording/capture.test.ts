import { describe, expect, it, vi } from 'vitest';
import { CaptureReceiver, DEFAULT_CAPTURE_WORKLET_URL, MAX_CAPTURE_SECONDS } from '../../src/recording/capture';
import type { MockGainNode } from '../mocks/audio-context';
import { RecordingContext, fakeEnvironment } from '../mocks/media';

/** Main-thread side of the capture (research R-01, FR-010). */
const SR = 44100;

interface Rig {
  readonly ctx: RecordingContext;
  readonly capture: CaptureReceiver;
  readonly limits: number[];
  readonly errors: string[];
}

function rig(url?: string): Rig {
  const ctx = new RecordingContext();
  const limits: number[] = [];
  const errors: string[] = [];
  const capture = new CaptureReceiver({
    context: ctx.asAudioContext(),
    env: fakeEnvironment(),
    ...(url === undefined ? {} : { workletUrl: url }),
    onLimit: () => limits.push(ctx.currentTime),
    onError: (reason) => errors.push(reason),
  });
  return { ctx, capture, limits, errors };
}

function block(frame: number, length: number, channels = 1): { type: 'block'; frame: number; channels: Float32Array[] } {
  return { type: 'block', frame, channels: Array.from({ length: channels }, () => new Float32Array(length).fill(0.25)) };
}

async function connected(channels: 1 | 2 = 1): Promise<Rig & { reply: (data: unknown) => void }> {
  const r = rig();
  await r.capture.connect(r.ctx.createGain() as unknown as AudioNode, channels);
  const node = r.ctx.captureNodes()[0];
  return { ...r, reply: (data) => node?.port.reply(data) };
}

describe('CaptureReceiver', () => {
  it('loads the worklet module once per context and creates the processor node with the channel count', async () => {
    const { ctx, capture } = rig();
    const input = ctx.createGain();
    await capture.connect(input as unknown as AudioNode, 2);
    await capture.connect(input as unknown as AudioNode, 1);
    expect(ctx.audioWorklet.modules).toEqual([DEFAULT_CAPTURE_WORKLET_URL]);
    expect(DEFAULT_CAPTURE_WORKLET_URL).toBe('./js/audio-engine-capture-worklet.js');
    const [first, second] = ctx.captureNodes();
    expect(first?.options).toMatchObject({ numberOfInputs: 1, channelCount: 2, channelCountMode: 'explicit', channelInterpretation: 'discrete', processorOptions: { channels: 2 } });
    expect([first?.disconnected, second?.options.processorOptions]).toEqual([true, { channels: 1 }]);
  });

  it('keeps the node pulled through a zero gain into the destination', async () => {
    const { ctx, capture } = rig('/custom.js');
    const input = ctx.createGain();
    await capture.connect(input as unknown as AudioNode, 1);
    const [node] = ctx.captureNodes();
    expect(input.connections).toContain(node);
    const keepAlive = node?.connections[0] as MockGainNode | undefined;
    expect([keepAlive?.gain.value, keepAlive?.connections[0]]).toEqual([0, ctx.destination]);
    expect(ctx.audioWorklet.modules).toEqual(['/custom.js']);
  });

  it('a failed module load rejects with unsupported: and is retried next time', async () => {
    const { ctx, capture } = rig();
    ctx.audioWorklet.error = new Error('404');
    await expect(capture.connect(ctx.createGain() as unknown as AudioNode, 1)).rejects.toThrow(/^unsupported: .*404/);
    ctx.audioWorklet.error = null;
    await capture.connect(ctx.createGain() as unknown as AudioNode, 1);
    expect(ctx.audioWorklet.modules).toHaveLength(2);
  });

  it('start posts start; blocks fill the session buffer with gaps; stop posts stop; take hands the buffer over', async () => {
    const r = await connected();
    r.reply(block(0, 2048));
    r.capture.start(MAX_CAPTURE_SECONDS);
    r.reply(block(4096, 2048));
    r.reply(block(8192, 2048));
    r.reply({ type: 'other' });
    r.reply(null);
    expect([r.capture.endFrame, r.capture.capturedFrames]).toEqual([10240, 4096]);
    r.capture.stop();
    const buffer = r.capture.take();
    expect(r.ctx.captureNodes()[0]?.port.posted).toEqual([{ type: 'start' }, { type: 'stop' }]);
    expect([buffer.firstFrame, buffer.endFrame, buffer.gaps]).toEqual([4096, 10240, 1]);
    expect([r.capture.capturedFrames, r.capture.endFrame]).toEqual([0, 0]);
  });

  it('keeps blocks that arrive after stop (the flushed tail) until take', async () => {
    const r = await connected();
    r.capture.start(10);
    r.reply(block(0, 2048));
    r.capture.stop();
    r.reply(block(2048, 300));
    expect(r.capture.endFrame).toBe(2348);
    r.capture.take();
    r.reply(block(2348, 2048));
    expect(r.capture.capturedFrames).toBe(0);
  });

  it('stops at the session limit (30 minutes by default) and reports it once', async () => {
    const r = await connected();
    expect(MAX_CAPTURE_SECONDS).toBe(1800);
    r.capture.start(1);
    r.reply(block(0, SR - 100));
    r.reply(block(SR - 100, 2048));
    r.reply(block(SR + 1948, 2048));
    expect([r.limits.length, r.capture.capturedFrames]).toEqual([1, SR]);
  });

  it('release drops the audio; disconnect drops the node and is idempotent', async () => {
    const r = await connected(2);
    expect(rig().capture.take().empty).toBe(true);
    r.capture.start(10);
    r.reply(block(0, 2048, 2));
    r.capture.release();
    expect(r.capture.capturedFrames).toBe(0);
    r.capture.disconnect();
    r.capture.disconnect();
    expect(r.ctx.captureNodes()[0]?.disconnected).toBe(true);
    expect(() => {
      r.capture.start(10);
      r.capture.stop();
    }).not.toThrow();
  });

  it('a processor error is reported', async () => {
    const r = await connected();
    r.ctx.captureNodes()[0]?.onprocessorerror?.(new Event('processorerror'));
    expect(r.errors).toEqual(['processorError']);
  });

  it('uses the context rate for the limit and a no-op error handler by default', async () => {
    const ctx = new RecordingContext();
    const capture = new CaptureReceiver({ context: ctx.asAudioContext(), env: fakeEnvironment(), onLimit: vi.fn() });
    await capture.connect(ctx.createGain() as unknown as AudioNode, 1);
    expect(() => ctx.captureNodes()[0]?.onprocessorerror?.(new Event('processorerror'))).not.toThrow();
  });
});
