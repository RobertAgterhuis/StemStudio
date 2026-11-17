import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CaptureReceiver } from '../../src/recording/capture';
import { Recorder, newUuid, validateRecordOptions, type RecorderHost } from '../../src/recording/recorder';
import { CaptureStore, describeTake } from '../../src/recording/takes';
import type { RecordingGating } from '../../src/recording/compare';
import { Transport } from '../../src/transport';
import type { CapturedTake, PracticeStatus, RecordOptions, RecordingProgress, RecordingState, StemName } from '../../src/types';
import { RecordingContext, fakeEnvironment } from '../mocks/media';

/** The recording state machine (contract 2.5.0, data-model 3.1, FR-004, FR-005, FR-009). */
const SR = 44100;
const L = 441;
const OPTIONS: RecordOptions = { latencySamples: L, latencyMethod: 'loopback', part: 'guitar', muteOriginal: true, countInBars: 0 };

function status(partial: Partial<PracticeStatus> = {}): PracticeStatus {
  return {
    speedPercent: 100,
    transposeSemitones: 0,
    stretch: 'bypass',
    stretchReason: null,
    latencyMs: 0,
    underruns: 0,
    trainer: { state: 'off', pass: 0, speedPercent: 100 },
    countIn: { available: true, active: false, beatsRemaining: 0, beatsTotal: 4 },
    ...partial,
  };
}

interface Rig {
  readonly ctx: RecordingContext;
  readonly transport: Transport;
  readonly recorder: Recorder;
  readonly store: CaptureStore;
  readonly states: RecordingState[];
  readonly progress: RecordingProgress[];
  readonly captured: CapturedTake[][];
  readonly gatings: (RecordingGating | null)[];
  readonly overrides: (number | null)[];
  practice: PracticeStatus;
  /** Context frame up to which input blocks were delivered. */
  fed: number;
  reply: (data: unknown) => void;
  inputOpen: boolean;
}

async function makeRig(seconds = 60): Promise<Rig> {
  const ctx = new RecordingContext();
  const transport = new Transport({ context: ctx.asAudioContext(), startDelaySeconds: 0.1, output: () => ctx.createGain() as unknown as AudioNode });
  const buffer = ctx.createBuffer(1, seconds * SR, SR) as unknown as AudioBuffer;
  transport.load(new Map<StemName, AudioBuffer>([['vocals', buffer]]));
  const capture = new CaptureReceiver({ context: ctx.asAudioContext(), env: fakeEnvironment(), onLimit: () => void rig.recorder.stop() });
  await capture.connect(ctx.createGain() as unknown as AudioNode, 1);
  const store = new CaptureStore();
  const rig = {
    ctx,
    transport,
    store,
    states: [],
    progress: [],
    captured: [],
    gatings: [],
    overrides: [],
    practice: status(),
    fed: 0,
    reply: (data: unknown) => ctx.captureNodes()[0]?.port.reply(data),
    inputOpen: true,
  } as unknown as Rig;
  let override: number | null = null;
  const host: RecorderHost = {
    context: ctx.asAudioContext(),
    transport,
    capture,
    store,
    input: {
      get isOpen() {
        return rig.inputOpen;
      },
      info: { deviceId: 'usb', label: 'USB', channelCount: 2, mode: 'mono-1', deviceSampleRate: 48000, estimatedLatencySamples: 0, outputLatencySamples: 0 },
      channels: 1,
    },
    practice: {
      status: () => rig.practice,
      overrideCountIn: (bars) => {
        override = bars;
        rig.overrides.push(bars);
      },
      play: () => transport.play(override !== null && override > 0 ? ctx.currentTime + 0.1 + override * 2 : undefined),
    },
    gating: (gating) => rig.gatings.push(gating),
    events: {
      state: (state) => rig.states.push(state),
      progress: (progress) => rig.progress.push(progress),
      takes: (takes) => rig.captured.push(takes),
    },
  };
  Object.assign(rig, { recorder: new Recorder(host) });
  transport.addListener(rig.recorder);
  rig.recorder.inputOpened();
  rig.states.length = 0;
  return rig;
}

/** Delivers capture blocks (sample = frame / 2^24) up to context frame `to`. */
function feed(rig: Rig, to: number): void {
  while (rig.fed + 2048 <= to) {
    const frame = rig.fed;
    rig.reply({ type: 'block', frame, channels: [Float32Array.from({ length: 2048 }, (_, i) => (frame + i) / 2 ** 24)] });
    rig.fed += 2048;
  }
}

/** Advances the audio clock, the capture and the timers in 25 ms steps. */
async function run(rig: Rig, seconds: number): Promise<void> {
  for (let i = 0; i < Math.round(seconds / 0.025); i++) {
    rig.ctx.currentTime += 0.025;
    feed(rig, Math.floor(rig.ctx.currentTime * SR));
    await vi.advanceTimersByTimeAsync(25);
  }
}

/** Calls stop and runs the clock until it resolves. */
async function stopped(rig: Rig): Promise<CapturedTake[]> {
  const box: { takes?: CapturedTake[] } = {};
  void rig.recorder.stop().then((takes) => {
    box.takes = takes;
  });
  for (let i = 0; i < 200 && box.takes === undefined; i++) {
    await run(rig, 0.025);
  }
  if (box.takes === undefined) {
    throw new Error('stop never resolved');
  }
  return box.takes;
}

/** The only take of the only session. */
function onlyTake(rig: Rig): CapturedTake {
  const take = rig.captured[0]?.[0];
  if (take === undefined) {
    throw new Error('no take');
  }
  return take;
}

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('validateRecordOptions', () => {
  it('fills the defaults (50 passes, 1800 s)', () => {
    expect(validateRecordOptions(OPTIONS)).toEqual({ ...OPTIONS, maxPasses: 50, maxSeconds: 1800 });
  });

  it.each([
    [{ latencySamples: -1 }, 'latencySamples'],
    [{ latencySamples: 44101 }, 'latencySamples'],
    [{ latencySamples: 1.5 }, 'latencySamples'],
    [{ latencyMethod: 'guess' }, 'latencyMethod'],
    [{ part: 'kazoo' }, 'part'],
    [{ countInBars: 3 }, 'countInBars'],
    [{ maxPasses: 0 }, 'maxPasses'],
    [{ maxPasses: 51 }, 'maxPasses'],
    [{ maxSeconds: 0 }, 'maxSeconds'],
    [{ maxSeconds: 1801 }, 'maxSeconds'],
  ])('rejects %o', (patch, field) => {
    expect(() => validateRecordOptions({ ...OPTIONS, ...patch } as RecordOptions)).toThrow(`Invalid record options: ${field}`);
  });
});

describe('Recorder', () => {
  it('records from the current position: states, gating, one compensated take, then back to open', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    expect([rig.recorder.state, rig.transport.state, rig.gatings]).toEqual(['recording', 'playing', [{ part: 'guitar', muteOriginal: true }]]);
    await run(rig, 2);
    await stopped(rig);
    expect(rig.states).toEqual(['recording', 'finishing', 'open']);
    expect(rig.captured[0]).toHaveLength(1);
    const take = onlyTake(rig);
    expect(take).toMatchObject({ passIndex: null, partial: false, startSample: 0, channels: 1, inputMode: 'mono-1', latencySamples: L, latencyMethod: 'loopback', dropouts: 0 });
    expect(take.lengthSamples).toBe(Math.round(2 * SR) - Math.round(0.1 * SR));
    expect([take.captureKey, take.sessionId].map((id) => /^[0-9a-f-]{36}$/.test(id))).toEqual([true, true]);
    expect(take.recordedAt).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    expect([rig.gatings.at(-1), rig.transport.state]).toEqual([null, 'paused']);
    const [first] = rig.store.samples(take.captureKey) ?? [];
    expect(Math.round((first?.[0] ?? 0) * 2 ** 24)).toBe(Math.round(0.1 * SR) + L);
  });

  it('starts with the count-in of the session and restores the stored count-in afterwards', async () => {
    const rig = await makeRig();
    rig.practice = status({ countIn: { available: true, active: true, beatsRemaining: 4, beatsTotal: 4 } });
    await rig.recorder.start({ ...OPTIONS, countInBars: 1 });
    expect([rig.recorder.state, rig.overrides]).toEqual(['countIn', [1]]);
    await run(rig, 0.5);
    expect(rig.progress.at(-1)).toMatchObject({ countInBeatsLeft: 4, passIndex: null });
    rig.practice = status();
    await run(rig, 2);
    expect(rig.recorder.state).toBe('recording');
    const [take] = await stopped(rig);
    expect([rig.overrides, take?.startSample]).toEqual([[1, null], 0]);
    expect(rig.states).toEqual(['countIn', 'recording', 'finishing', 'open']);
  });

  it('a count-in without a usable tempo goes straight to recording', async () => {
    const rig = await makeRig();
    rig.practice = status({ countIn: { available: false, active: false, beatsRemaining: 0, beatsTotal: 8 } });
    await rig.recorder.start({ ...OPTIONS, countInBars: 2 });
    expect(rig.recorder.state).toBe('recording');
  });

  it.each([
    [{ speedPercent: 90 }, /^recordingSpeed: /],
    [{ transposeSemitones: 2 }, /^recordingSpeed: /],
    [{ trainer: { state: 'waiting', pass: 1, speedPercent: 70 } }, /^speedTrainer: /],
  ] as const)('refuses %o', async (patch, message) => {
    const rig = await makeRig();
    rig.practice = status(patch);
    await expect(rig.recorder.start(OPTIONS)).rejects.toThrow(message);
    expect([rig.recorder.state, rig.states]).toEqual(['open', []]);
  });

  it('refuses without an open input, without a track and while already recording', async () => {
    const closed = await makeRig();
    closed.recorder.inputClosed();
    await expect(closed.recorder.start(OPTIONS)).rejects.toThrow('Open an input first');
    const empty = await makeRig();
    empty.transport.load(new Map());
    await expect(empty.recorder.start(OPTIONS)).rejects.toThrow('Load a track first');
    const busy = await makeRig();
    await busy.recorder.start(OPTIONS);
    await expect(busy.recorder.start(OPTIONS)).rejects.toThrow('A recording is already running');
    expect(busy.recorder.busy).toBe(true);
  });

  it('pauses a playing transport first and records from its position', async () => {
    const rig = await makeRig();
    rig.transport.seek(5);
    await rig.transport.play();
    await run(rig, 1);
    await rig.recorder.start(OPTIONS);
    await run(rig, 1);
    const [take] = await stopped(rig);
    expect(Math.abs((take?.startSample ?? 0) - Math.round(5.9 * SR))).toBeLessThanOrEqual(2);
  });

  it('finishing waits for L + one batch of capture after the stop', async () => {
    const rig = await makeRig();
    await rig.recorder.start({ ...OPTIONS, latencySamples: 22050 });
    await run(rig, 1);
    let done = false;
    void rig.recorder.stop().then(() => {
      done = true;
    });
    await vi.advanceTimersByTimeAsync(100);
    expect([done, rig.recorder.state]).toEqual([false, 'finishing']);
    await run(rig, 0.6);
    expect(done).toBe(true);
    expect(onlyTake(rig).lengthSamples).toBe(Math.round(1 * SR) - Math.round(0.1 * SR));
  });

  it('gives up waiting when no capture arrives and keeps what was captured', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    await run(rig, 1);
    const pending = rig.recorder.stop();
    await vi.advanceTimersByTimeAsync(3000);
    const takes = await pending;
    expect(takes).toHaveLength(1);
  });

  it('stops at the end of the song and keeps the take', async () => {
    const rig = await makeRig(2);
    await rig.recorder.start(OPTIONS);
    await run(rig, 2.2);
    rig.ctx.sources.at(-1)?.end();
    await run(rig, 0.5);
    expect(onlyTake(rig).lengthSamples).toBe(2 * SR);
  });

  it('a pause from the user ends the recording; stop while idle returns nothing', async () => {
    const rig = await makeRig();
    expect(await rig.recorder.stop()).toEqual([]);
    await rig.recorder.start(OPTIONS);
    await run(rig, 1);
    rig.transport.pause();
    for (let i = 0; i < 40 && rig.captured.length === 0; i++) {
      await run(rig, 0.025);
    }
    expect(rig.captured).toHaveLength(1);
  });

  it('stop twice shares one session end', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    await run(rig, 1);
    const first = rig.recorder.stop();
    const second = rig.recorder.stop();
    for (let i = 0; i < 40; i++) {
      await run(rig, 0.025);
    }
    expect(await first).toBe(await second);
    expect(rig.captured).toHaveLength(1);
  });

  it('reports progress at most 4 Hz while recording', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    await run(rig, 2);
    expect(rig.progress.length).toBeGreaterThanOrEqual(7);
    expect(rig.progress.length).toBeLessThanOrEqual(9);
    expect(rig.progress.at(-1)?.elapsedSeconds).toBeGreaterThan(1.7);
    expect(rig.progress.at(-1)?.countInBeatsLeft).toBeNull();
  });

  it('records one take per loop pass and stops after maxPasses', async () => {
    const rig = await makeRig();
    rig.transport.setLoop({ start: 1, end: 2, enabled: true });
    await rig.recorder.start({ ...OPTIONS, maxPasses: 2 });
    await run(rig, 1.5);
    expect(rig.progress.at(-1)?.passIndex).toBe(2);
    await run(rig, 1.5);
    expect(rig.captured).toHaveLength(1);
    expect(rig.captured[0]?.map((t) => [t.passIndex, t.partial, t.startSample, t.lengthSamples])).toEqual([
      [1, false, SR, SR],
      [2, false, SR, SR],
    ]);
  });

  it('an input loss stops the session, keeps a partial take and ends closed', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    await run(rig, 1);
    rig.inputOpen = false;
    const pending = rig.recorder.inputLost();
    await vi.advanceTimersByTimeAsync(3000);
    const takes = await pending;
    expect([takes[0]?.partial, rig.recorder.state, rig.states.at(-1)]).toEqual([true, 'closed', 'closed']);
  });

  it('inputLost while idle closes; inputOpened/inputClosed set open/closed', async () => {
    const rig = await makeRig();
    expect(await rig.recorder.inputLost()).toEqual([]);
    expect(rig.recorder.state).toBe('closed');
    rig.recorder.inputOpened();
    rig.recorder.inputClosed();
    expect(rig.states).toEqual(['closed', 'open', 'closed']);
  });

  it('input open/close events do not change the state while a session runs', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    rig.recorder.inputClosed();
    rig.recorder.inputOpened();
    expect(rig.recorder.state).toBe('recording');
    const pending = rig.recorder.stop();
    await vi.advanceTimersByTimeAsync(300);
    expect(rig.recorder.state).toBe('finishing');
    await vi.advanceTimersByTimeAsync(3000);
    await pending;
  });

  it('dispose stops the progress timer and detaches from the transport', async () => {
    const rig = await makeRig();
    await rig.recorder.start(OPTIONS);
    rig.recorder.dispose();
    const before = rig.progress.length;
    await run(rig, 1);
    expect(rig.progress.length).toBe(before);
  });
});

describe('takes', () => {
  it('newUuid falls back to getRandomValues (v4 layout)', () => {
    vi.stubGlobal('crypto', { getRandomValues: (bytes: Uint8Array) => bytes.fill(171) });
    expect(newUuid()).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-a[0-9a-f]{3}-[0-9a-f]{12}$/);
    vi.unstubAllGlobals();
  });

  it('describeTake: stereo channels, peak and clipped samples (abs >= (2^23 - 1) / 2^23)', () => {
    const channels = [Float32Array.from([0.5, 1, -1]), Float32Array.from([0, 0.25, 0.9999999])];
    const take = describeTake(
      { passIndex: 2, partial: false, startSample: 10, lengthSamples: 3, firstFrame: 100, channels, dropouts: 1 },
      { sessionId: 's', inputMode: 'stereo', latencySamples: 500, latencyMethod: 'manual', recordedAt: 'now' },
      'k',
    );
    expect(take).toEqual({
      captureKey: 'k',
      sessionId: 's',
      passIndex: 2,
      partial: false,
      startSample: 10,
      lengthSamples: 3,
      channels: 2,
      inputMode: 'stereo',
      latencySamples: 500,
      latencyMethod: 'manual',
      peakDbfs: 0,
      clippedSamples: 3,
      dropouts: 1,
      recordedAt: 'now',
    });
  });
});

describe('CaptureStore', () => {
  it('captureWav returns a 24-bit WAV blob; releaseCapture frees it; unknown keys', async () => {
    const store = new CaptureStore();
    store.put('k', [new Float32Array(10), new Float32Array(10)]);
    const blob = store.wav('k');
    expect([blob.type, blob.size]).toEqual(['audio/wav', 44 + 10 * 6]);
    expect(new Uint8Array(await blob.arrayBuffer()).slice(0, 4)).toEqual(new Uint8Array([82, 73, 70, 70]));
    store.release('k');
    store.release('k');
    expect(() => store.wav('k')).toThrow('Unknown capture "k"');
    expect(store.samples('k')).toBeUndefined();
    store.put('a', [new Float32Array(1)]);
    store.clear();
    expect(store.samples('a')).toBeUndefined();
  });
});
