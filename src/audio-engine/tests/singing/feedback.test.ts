import { beforeEach, describe, expect, it } from 'vitest';
import { PITCH_PROCESSOR_NAME } from '../../src/pitch/worklet';
import { OVERRUN_REASON, SingingFeedback, type SingingSession, type SessionSpec } from '../../src/singing/feedback';
import type { InputInfo, SingingFeedbackRequest, SingingState, SingingSummary } from '../../src/types';
import { MockAudioNode } from '../mocks/audio-context';
import { WorkletContext, type MockAudioWorkletNode } from '../mocks/worklet-node';

const INFO: InputInfo = { deviceId: 'usb', label: 'USB', channelCount: 1, mode: 'mono-1', deviceSampleRate: 48_000, estimatedLatencySamples: 1323, outputLatencySamples: 441 };

const REQUEST: SingingFeedbackRequest = {
  pitchViewElementId: 'pitch-view',
  reference: { hopMs: 10, frames: [0, 6900, 6900, 6900] },
  units: [{ index: 0, startMs: 0, endMs: 1000 }],
  toleranceCents: 100,
};

/** Fake session (scorer + pitch view) recording what the feedback hands it. */
class FakeSession implements SingingSession {
  public readonly frames: [number, number | null][] = [];
  public tolerance: number;
  public stopped = false;
  public disposed = false;

  public constructor(public readonly spec: SessionSpec) {
    this.tolerance = spec.request.toleranceCents;
  }

  public frame(songMs: number, cents: number | null): void {
    this.frames.push([songMs, cents]);
  }

  public readonly states: SingingState[] = [];
  public repositions = 0;

  public setTolerance(cents: number): void {
    this.tolerance = cents;
  }

  public setState(state: SingingState): void {
    this.states.push(state);
  }

  public reposition(): void {
    this.repositions += 1;
  }

  public summary(): Omit<SingingSummary, 'latencySource'> {
    return { unitsScored: 1, average: 80, best: { index: 0, score: 80 }, toleranceCents: this.tolerance };
  }

  public stop(): void {
    this.stopped = true;
  }

  public dispose(): void {
    this.disposed = true;
  }
}

interface Rig {
  readonly ctx: WorkletContext;
  readonly feedback: SingingFeedback;
  readonly states: [SingingState, string | null][];
  readonly sessions: FakeSession[];
  readonly input: { current: { node: AudioNode; info: InputInfo } | null };
  readonly clock: { playing: boolean };
}

function makeRig(): Rig {
  document.body.replaceChildren();
  const host = document.createElement('div');
  host.id = 'pitch-view';
  document.body.append(host);
  const ctx = new WorkletContext();
  const states: [SingingState, string | null][] = [];
  const sessions: FakeSession[] = [];
  const input: Rig['input'] = { current: { node: new MockAudioNode(ctx, 'input') as unknown as AudioNode, info: INFO } };
  const clock = { playing: true };
  const feedback = new SingingFeedback({
    context: ctx.asAudioContext(),
    createNode: (context, name, options) => (context as unknown as WorkletContext).createWorkletNode(name, options) as unknown as AudioWorkletNode,
    input: () => input.current,
    workletUrl: '/p.js',
    positionAtMs: (time) => Math.round(time * 1000),
    playing: () => clock.playing,
    notify: { state: (state, reason) => states.push([state, reason]), unit: () => undefined },
    session: (spec) => {
      const session = new FakeSession(spec);
      sessions.push(session);
      return session;
    },
    document,
  });
  return { ctx, feedback, states, sessions, input, clock };
}

function node(rig: Rig): MockAudioWorkletNode {
  const created = rig.ctx.workletNodes.at(-1);
  if (created === undefined) {
    throw new Error('no worklet node');
  }
  return created;
}

/** `count` ordered 10 ms units. */
function manyUnits(count: number): { index: number; startMs: number; endMs: number }[] {
  return Array.from({ length: count }, (_, i) => ({ index: i, startMs: i * 10, endMs: i * 10 + 10 }));
}

/** Replaces the module load with a promise the test settles. */
function deferLoad(): { resolve: () => void; reject: (error: Error) => void } {
  const settle = { resolve: (): void => undefined, reject: (_: Error): void => undefined };
  rig.ctx.audioWorklet.addModule = (url: string): Promise<void> => {
    rig.ctx.audioWorklet.modules.push(url);
    return new Promise<void>((resolve, reject) => {
      settle.resolve = resolve;
      settle.reject = reject;
    });
  };
  return settle;
}

let rig: Rig;

beforeEach(() => {
  rig = makeRig();
});

describe('startSingingFeedback', () => {
  it('loads the pitch-tracker module once, connects the open input and goes starting -> running', async () => {
    await rig.feedback.start(REQUEST);
    rig.feedback.stop();
    await rig.feedback.start(REQUEST);
    expect(rig.ctx.audioWorklet.modules).toEqual(['/p.js']);
    expect(rig.ctx.workletNodes.map((n) => n.name)).toEqual([PITCH_PROCESSOR_NAME, PITCH_PROCESSOR_NAME]);
    expect(node(rig).options.numberOfOutputs).toBe(0);
    expect((rig.input.current?.node as unknown as MockAudioNode).connections.at(-1)).toBe(node(rig));
    expect(rig.states).toEqual([['starting', null], ['running', null], ['stopped', null], ['starting', null], ['running', null]]);
    expect(rig.feedback.state).toBe('running');
  });

  it('uses the default worklet URL', async () => {
    const ctx = new WorkletContext();
    const feedback = new SingingFeedback({
      context: ctx.asAudioContext(),
      createNode: (context, name, options) => (context as unknown as WorkletContext).createWorkletNode(name, options) as unknown as AudioWorkletNode,
      input: () => rig.input.current,
      positionAtMs: () => 0,
      playing: () => false,
      notify: { state: () => undefined, unit: () => undefined },
      session: (spec) => new FakeSession(spec),
    });
    await feedback.start(REQUEST);
    expect(ctx.audioWorklet.modules).toEqual(['./js/audio-engine-pitch-worklet.js']);
  });

  it('rejects without an open input and stays idle', async () => {
    rig.input.current = null;
    await expect(rig.feedback.start(REQUEST)).rejects.toThrow('No input device is open');
    expect(rig.states).toEqual([]);
    expect(rig.feedback.state).toBe('idle');
  });

  it('rejects while running', async () => {
    await rig.feedback.start(REQUEST);
    await expect(rig.feedback.start(REQUEST)).rejects.toThrow('Singing feedback is already running');
  });

  it('rejects an unknown pitch view host', async () => {
    await expect(rig.feedback.start({ ...REQUEST, pitchViewElementId: 'nope' })).rejects.toThrow('Pitch view host not found: nope');
  });

  it.each([
    ['tolerance', { ...REQUEST, toleranceCents: 5 }, 'Invalid singing request: tolerance outside 10..200'],
    ['units order', { ...REQUEST, units: [{ index: 0, startMs: 500, endMs: 900 }, { index: 1, startMs: 100, endMs: 400 }] }, 'Invalid singing request: units not ordered'],
    ['unit range', { ...REQUEST, units: [{ index: 0, startMs: 500, endMs: 500 }] }, 'Invalid singing request: units not ordered'],
    ['units missing', { ...REQUEST, units: null }, 'Invalid singing request: units missing'],
    ['round trip', { ...REQUEST, roundTripMs: -1 }, 'Invalid singing request: roundTripMs outside 0..1000'],
    ['hop', { ...REQUEST, reference: { hopMs: 20, frames: [] } }, 'Invalid pitch contour: hopMs not 10'],
    ['frames', { ...REQUEST, reference: { hopMs: 10, frames: new Int16Array(90_001) } }, 'Invalid pitch contour: more than 90000 frames'],
    ['value', { ...REQUEST, reference: { hopMs: 10, frames: [12_701] } }, 'Invalid pitch contour: values outside 0..12700'],
    ['missing frames', { ...REQUEST, reference: { hopMs: 10, frames: null } }, 'Invalid pitch contour: frames missing'],
    ['latency method', { ...REQUEST, roundTripMs: 50, latencyMethod: 'x' }, 'Invalid singing request: latencyMethod'],
    ['latency method type', { ...REQUEST, latencyMethod: 7 }, 'Invalid singing request: latencyMethod'],
    ['unit count', { ...REQUEST, units: manyUnits(100_001) }, 'Invalid singing request: more than 20000 units'],
    ['negative index', { ...REQUEST, units: [{ index: -1, startMs: 0, endMs: 10 }] }, 'Invalid singing request: unit index invalid'],
    ['fractional index', { ...REQUEST, units: [{ index: 1.5, startMs: 0, endMs: 10 }] }, 'Invalid singing request: unit index invalid'],
    ['null unit', { ...REQUEST, units: [null] }, 'Invalid singing request: unit index invalid'],
    ['NaN start', { ...REQUEST, units: [{ index: 0, startMs: Number.NaN, endMs: 10 }] }, 'Invalid singing request: unit times not finite'],
    ['infinite end', { ...REQUEST, units: [{ index: 0, startMs: 0, endMs: Number.POSITIVE_INFINITY }] }, 'Invalid singing request: unit times not finite'],
    ['string start', { ...REQUEST, units: [{ index: 0, startMs: '0', endMs: 10 }] }, 'Invalid singing request: unit times not finite'],
  ])('rejects an invalid %s', async (_, request, message) => {
    await expect(rig.feedback.start(request as unknown as SingingFeedbackRequest)).rejects.toThrow(message);
    expect(rig.states).toEqual([]);
  });

  it('a failing module load fails the state, rejects, and the next start retries the module', async () => {
    rig.ctx.audioWorklet.error = new Error('404');
    await expect(rig.feedback.start(REQUEST)).rejects.toThrow('Pitch tracking could not start (Error: 404)');
    expect(rig.states.at(-1)).toEqual(['failed', 'Pitch tracking could not start (Error: 404)']);
    rig.ctx.audioWorklet.error = null;
    await rig.feedback.start(REQUEST);
    expect(rig.ctx.audioWorklet.modules).toHaveLength(2);
    expect(rig.feedback.state).toBe('running');
  });
});

describe('stop or dispose while the module loads', () => {
  it.each([
    ['stop', (): void => void rig.feedback.stop()],
    ['dispose', (): void => {
      rig.feedback.dispose();
    }],
  ])('%s cancels the start: no tracker, no session, state stopped, no running notification', async (_, cancel) => {
    const load = deferLoad();
    const starting = rig.feedback.start(REQUEST);
    cancel();
    load.resolve();
    await starting;
    expect(rig.ctx.workletNodes).toEqual([]);
    expect(rig.sessions).toEqual([]);
    expect((rig.input.current?.node as unknown as MockAudioNode).connections).toEqual([]);
    expect(rig.feedback.state).toBe('stopped');
    expect(rig.states).toEqual([['starting', null], ['stopped', null]]);
  });

  it('a failed load after a stop rejects but keeps the state stopped', async () => {
    const load = deferLoad();
    const starting = rig.feedback.start(REQUEST);
    rig.feedback.stop();
    load.reject(new Error('404'));
    await expect(starting).rejects.toThrow('Pitch tracking could not start (Error: 404)');
    expect(rig.feedback.state).toBe('stopped');
    expect(rig.states).toEqual([['starting', null], ['stopped', null]]);
  });

  it('a new start after a cancelled one runs, the stale load connects nothing', async () => {
    const load = deferLoad();
    const first = rig.feedback.start(REQUEST);
    rig.feedback.stop();
    const second = rig.feedback.start(REQUEST);
    load.resolve();
    await Promise.all([first, second]);
    expect(rig.ctx.workletNodes).toHaveLength(1);
    expect(rig.sessions).toHaveLength(1);
    expect(rig.feedback.state).toBe('running');
  });
});

describe('pitch frames', () => {
  it('applies a 5-frame median over voiced values, null breaks the line', async () => {
    await rig.feedback.start(REQUEST);
    const values = [6900, 7000, 6950, 6000, 6910, null, 6800];
    values.forEach((cents, i) => {
      node(rig).port.reply({ t: 1 + i * 0.01, cents, clarity: 0.9 });
    });
    expect(rig.sessions[0]?.frames.map(([, c]) => c)).toEqual([6900, 6950, 6950, 6925, 6910, null, 6800]);
  });

  it('aligns frames to the song by the round trip of the request', async () => {
    await rig.feedback.start({ ...REQUEST, roundTripMs: 120, latencyMethod: 'loopback' });
    node(rig).port.reply({ t: 2, cents: 6900, clarity: 1 });
    expect(rig.sessions[0]?.frames[0]?.[0]).toBe(1880);
    expect(rig.sessions[0]?.spec.latency).toEqual({ ms: 120, source: 'loopback' });
  });

  it('uses the input estimate without a calibration', async () => {
    await rig.feedback.start(REQUEST);
    node(rig).port.reply({ t: 2, cents: 6900, clarity: 1 });
    expect(rig.sessions[0]?.frames[0]?.[0]).toBe(1970);
  });

  it('ignores frames while the transport is not playing and malformed messages', async () => {
    await rig.feedback.start(REQUEST);
    rig.clock.playing = false;
    node(rig).port.reply({ t: 2, cents: 6900, clarity: 1 });
    rig.clock.playing = true;
    node(rig).port.reply(null);
    node(rig).port.reply({ t: 'x' });
    expect(rig.sessions[0]?.frames).toEqual([]);
  });
});

describe('overrun guard', () => {
  function lagging(count: number, spacing: number): void {
    for (let i = 0; i < count; i += 1) {
      rig.ctx.currentTime = 10 + i * spacing;
      node(rig).port.reply({ t: rig.ctx.currentTime - 0.06, cents: null, clarity: 0 });
    }
  }

  it('fails after 50 hops lagging more than 50 ms within 5 s', async () => {
    await rig.feedback.start(REQUEST);
    const tracker = node(rig);
    lagging(49, 0.01);
    expect(rig.feedback.state).toBe('running');
    lagging(1, 0.01);
    expect(rig.feedback.state).toBe('failed');
    expect(rig.states.at(-1)).toEqual(['failed', OVERRUN_REASON]);
    expect(OVERRUN_REASON).toBe('Pitch tracking is too slow on this device.');
    expect(tracker.disconnected).toBe(true);
    expect(rig.sessions[0]?.stopped).toBe(true);
  });

  it('does not fail when the lagging hops are spread over more than 5 s', async () => {
    await rig.feedback.start(REQUEST);
    lagging(60, 0.2);
    expect(rig.feedback.state).toBe('running');
  });
});

describe('stopSingingFeedback', () => {
  it('stops the worklet, disconnects it, keeps the view and returns the summary; idempotent', async () => {
    await rig.feedback.start(REQUEST);
    const tracker = node(rig);
    const summary = rig.feedback.stop();
    expect(tracker.port.posted).toEqual([{ type: 'stop' }]);
    expect(tracker.disconnected).toBe(true);
    expect(rig.sessions[0]?.stopped).toBe(true);
    expect(rig.sessions[0]?.disposed).toBe(false);
    expect(summary).toEqual({ unitsScored: 1, average: 80, best: { index: 0, score: 80 }, toleranceCents: 100, latencySource: 'estimated' });
    expect(rig.feedback.stop()).toEqual(summary);
    expect(rig.states.filter(([s]) => s === 'stopped')).toHaveLength(1);
  });

  it('returns an empty summary before any start', () => {
    expect(rig.feedback.stop()).toEqual({ unitsScored: 0, average: null, best: null, toleranceCents: 100, latencySource: 'estimated' });
    expect(rig.states).toEqual([]);
  });

  it('a new start disposes the previous session (its canvas)', async () => {
    await rig.feedback.start(REQUEST);
    rig.feedback.stop();
    await rig.feedback.start(REQUEST);
    expect(rig.sessions[0]?.disposed).toBe(true);
  });

  it('dispose stops a running feedback and removes the view', async () => {
    await rig.feedback.start(REQUEST);
    rig.feedback.dispose();
    expect(rig.sessions[0]?.disposed).toBe(true);
    expect(node(rig).disconnected).toBe(true);
  });
});

describe('setSingingTolerance', () => {
  it('forwards 10..200 to the running session and rejects other values', async () => {
    await rig.feedback.start(REQUEST);
    rig.feedback.setTolerance(25);
    expect(rig.sessions[0]?.tolerance).toBe(25);
    expect(() => {
      rig.feedback.setTolerance(201);
    }).toThrow('Invalid singing request: tolerance outside 10..200');
    expect(rig.feedback.stop().toleranceCents).toBe(25);
  });

  it('is kept for the empty summary without a session', () => {
    rig.feedback.setTolerance(50);
    expect(rig.feedback.stop().toleranceCents).toBe(50);
  });
});

describe('getRoundTripLatency', () => {
  it('is the calibration of the running request, else the open input estimate, else 30 ms', async () => {
    expect(rig.feedback.roundTrip()).toEqual({ ms: 30, source: 'estimated' });
    await rig.feedback.start({ ...REQUEST, roundTripMs: 85 });
    expect(rig.feedback.roundTrip()).toEqual({ ms: 85, source: 'manual' });
    rig.feedback.stop();
    rig.input.current = null;
    expect(rig.feedback.roundTrip()).toEqual({ ms: 30, source: 'estimated' });
  });

  it('a null roundTripMs uses the input estimate (1323 samples = 30 ms)', async () => {
    await rig.feedback.start({ ...REQUEST, roundTripMs: null, latencyMethod: 'loopback' });
    expect(rig.feedback.roundTrip()).toEqual({ ms: 30, source: 'estimated' });
  });
});

describe('session state and seeks', () => {
  it('hands the state to the session and forwards seeks to the running session only', async () => {
    rig.feedback.reposition();
    await rig.feedback.start(REQUEST);
    rig.feedback.reposition();
    rig.feedback.stop();
    rig.feedback.reposition();
    expect(rig.sessions[0]?.states).toEqual(['running', 'stopped']);
    expect(rig.sessions[0]?.repositions).toBe(1);
  });
});
