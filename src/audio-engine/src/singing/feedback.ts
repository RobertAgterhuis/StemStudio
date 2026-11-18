/**
 * Live singing feedback (contract 2.6.0 `startSingingFeedback`, `setSingingTolerance`, `stopSingingFeedback`,
 * `getRoundTripLatency`; data-model 3.2-3.3, research R-05/R-07): loads the `pitch-tracker` worklet once per context,
 * connects it to the open 015 input, smooths its frames with a 5-frame median, aligns them to the song with the round
 * trip and hands them to a session (scorer + pitch view). An overrun guard stops tracking on slow devices (FR-045).
 *
 * @packageDocumentation
 */
import { PITCH_PROCESSOR_NAME } from '../pitch/worklet';
import type { InputInfo, LatencyMethod, RoundTripLatency, SingingFeedbackRequest, SingingState, SingingSummary, SingingUnitScore } from '../types';
import { validateContour, validateRoundTrip, validateTolerance, validateUnits } from './validate';

/** Default URL of the pitch worklet module. */
export const DEFAULT_PITCH_WORKLET_URL = './js/audio-engine-pitch-worklet.js';
/** Failure reason of the overrun guard. */
export const OVERRUN_REASON = 'Pitch tracking is too slow on this device.';
/** A hop is late when its window lags the context time by more than this (s). */
const LATE_SECONDS = 0.05;
/** Late hops within {@link OVERRUN_WINDOW_SECONDS} that stop tracking. */
const OVERRUN_HOPS = 50;
/** Window of the overrun guard (s). */
const OVERRUN_WINDOW_SECONDS = 5;
/** Voiced frames in the median filter. */
const MEDIAN_FRAMES = 5;
/** Round trip without any input information (ms, research R-07). */
const FALLBACK_ROUND_TRIP_MS = 30;
/** Rate of the 015 latency samples (Hz). */
const LATENCY_RATE = 44.1;

/** A validated request plus its host and the latency used. */
export interface SessionSpec {
  /** The request (validated). */
  readonly request: SingingFeedbackRequest;
  /** Reference frames. */
  readonly reference: Int16Array;
  /** Pitch view host. */
  readonly host: HTMLElement;
  /** Round trip used for the alignment. */
  readonly latency: RoundTripLatency;
  /** Receives every finished unit. */
  readonly onUnit: (score: SingingUnitScore) => void;
}

/** Scorer and pitch view of one running feedback. */
export interface SingingSession {
  /** One smoothed, aligned singer frame (song ms, cents or null). */
  frame(songMs: number, cents: number | null): void;
  /** New tolerance for the current and later units. */
  setTolerance(cents: number): void;
  /** Summary without the latency source. */
  summary(): Omit<SingingSummary, 'latencySource'>;
  /** Stops following the song; the view keeps its last window. */
  stop(): void;
  /** Removes the view. */
  dispose(): void;
}

/** The open 015 input. */
export interface OpenInput {
  /** Selected-channel node. */
  readonly node: AudioNode;
  /** Its description (latency estimate). */
  readonly info: InputInfo;
}

/** Construction parameters of {@link SingingFeedback}. */
export interface SingingFeedbackDeps {
  readonly context: AudioContext;
  /** `new AudioWorkletNode(...)` (or a test fake). */
  readonly createNode: (context: AudioContext, name: string, options: AudioWorkletNodeOptions) => AudioWorkletNode;
  /** The open input, or null. */
  readonly input: () => OpenInput | null;
  /** Module URL; default {@link DEFAULT_PITCH_WORKLET_URL}. */
  readonly workletUrl?: string;
  /** Track ms at a context time (the timeline clock). */
  readonly positionAtMs: (contextTime: number) => number;
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** .NET notifications. */
  readonly notify: { state(state: SingingState, reason: string | null): void; unit(score: SingingUnitScore): void };
  /** Builds the scorer and pitch view. */
  readonly session: (spec: SessionSpec) => SingingSession;
  /** Document holding the pitch view host; default `globalThis.document`. */
  readonly document?: Document;
}

/** One running tracker. */
interface Running {
  readonly node: AudioWorkletNode;
  readonly session: SingingSession;
  readonly latency: RoundTripLatency;
  readonly median: number[];
  readonly late: number[];
}

interface FrameMessage {
  readonly t: number;
  readonly cents: number | null;
}

const modules = new WeakMap<BaseAudioContext, Promise<void>>();

function loadModule(context: AudioContext, url: string): Promise<void> {
  let loading = modules.get(context);
  if (loading === undefined) {
    loading = context.audioWorklet.addModule(url).catch((error: unknown) => {
      modules.delete(context);
      throw new Error(`Pitch tracking could not start (${String(error)})`, { cause: error });
    });
    modules.set(context, loading);
  }
  return loading;
}

function isFrame(data: unknown): data is FrameMessage {
  const frame = data as Partial<FrameMessage> | null;
  return typeof frame?.t === 'number' && (frame.cents === null || typeof frame.cents === 'number');
}

function middle(values: readonly number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const half = sorted.length >> 1;
  return sorted.length % 2 === 1 ? Number(sorted[half]) : (Number(sorted[half - 1]) + Number(sorted[half])) / 2;
}

/** Live singing feedback of one engine. */
export class SingingFeedback {
  private current: SingingState = 'idle';
  private running: Running | null = null;
  private session: SingingSession | null = null;
  private tolerance = 100;
  private last: SingingSummary | null = null;

  /** @param deps - Context, node factory, input, clock, notifications and session factory. */
  public constructor(private readonly deps: SingingFeedbackDeps) {}

  /** Current state. */
  public get state(): SingingState {
    return this.current;
  }

  /**
   * @param request - The request.
   * @throws Error (rejects) for an invalid request, a missing host or input, a running feedback or a failed load.
   */
  public async start(request: SingingFeedbackRequest): Promise<void> {
    const spec = this.prepare(request);
    this.setState('starting', null);
    try {
      await loadModule(this.deps.context, this.deps.workletUrl ?? DEFAULT_PITCH_WORKLET_URL);
    } catch (error) {
      this.setState('failed', (error as Error).message);
      throw error;
    }
    this.connect(spec);
    this.setState('running', null);
  }

  /** @param cents - Tolerance 10..200 for the current and later units. */
  public setTolerance(cents: number): void {
    this.tolerance = validateTolerance(cents);
    this.running?.session.setTolerance(cents);
  }

  /** @returns The summary of the last feedback; idempotent. */
  public stop(): SingingSummary {
    if (this.running !== null) {
      this.halt();
      this.setState('stopped', null);
    }
    return this.last ?? { unitsScored: 0, average: null, best: null, toleranceCents: this.tolerance, latencySource: 'estimated' };
  }

  /** @returns The round trip a feedback uses (running request, else the input estimate, else 30 ms). */
  public roundTrip(): RoundTripLatency {
    return this.running?.latency ?? this.estimate();
  }

  /** Stops a running feedback and removes the view (engine disposal). */
  public dispose(): void {
    if (this.running !== null) {
      this.halt();
    }
    this.session?.dispose();
    this.session = null;
  }

  private prepare(request: SingingFeedbackRequest): SessionSpec {
    const reference = validateContour(request.reference);
    validateUnits(request.units);
    validateTolerance(request.toleranceCents);
    const roundTripMs = validateRoundTrip(request.roundTripMs);
    this.assertCanStart();
    const host = this.host(request.pitchViewElementId);
    const onUnit = (score: SingingUnitScore): void => {
      this.deps.notify.unit(score);
    };
    return { request, reference, host, latency: this.latencyOf(roundTripMs, request.latencyMethod), onUnit };
  }

  private assertCanStart(): void {
    if (this.running !== null || this.current === 'starting') {
      throw new Error('Singing feedback is already running');
    }
    if (this.deps.input() === null) {
      throw new Error('No input device is open');
    }
  }

  private host(elementId: string): HTMLElement {
    const host = (this.deps.document ?? globalThis.document).getElementById(elementId);
    if (host === null) {
      throw new Error(`Pitch view host not found: ${elementId}`);
    }
    return host;
  }

  private latencyOf(roundTripMs: number | null, method: LatencyMethod | undefined): RoundTripLatency {
    return roundTripMs === null ? this.estimate() : { ms: roundTripMs, source: method ?? 'manual' };
  }

  private connect(spec: SessionSpec): void {
    const { context } = this.deps;
    const node = this.deps.createNode(context, PITCH_PROCESSOR_NAME, {
      numberOfInputs: 1,
      numberOfOutputs: 0,
      channelCount: 1,
      channelCountMode: 'explicit',
      channelInterpretation: 'speakers',
    });
    this.deps.input()?.node.connect(node);
    this.session?.dispose();
    this.tolerance = spec.request.toleranceCents;
    this.session = this.deps.session(spec);
    this.running = { node, session: this.session, latency: spec.latency, median: [], late: [] };
    node.port.onmessage = (event: MessageEvent): void => {
      this.receive(event.data);
    };
  }

  private receive(data: unknown): void {
    const running = this.running;
    if (running === null || !isFrame(data)) {
      return;
    }
    if (this.overrun(running, data.t)) {
      this.halt();
      this.setState('failed', OVERRUN_REASON);
      return;
    }
    if (this.deps.playing()) {
      const songMs = this.deps.positionAtMs(data.t - running.latency.ms / 1000);
      running.session.frame(songMs, this.smooth(running, data.cents));
    }
  }

  private smooth(running: Running, cents: number | null): number | null {
    if (cents === null) {
      running.median.length = 0;
      return null;
    }
    running.median.push(cents);
    if (running.median.length > MEDIAN_FRAMES) {
      running.median.shift();
    }
    return middle(running.median);
  }

  /** Counts late hops; true when 50 of them fell within 5 s. */
  private overrun(running: Running, t: number): boolean {
    const now = this.deps.context.currentTime;
    if (now - t > LATE_SECONDS) {
      running.late.push(now);
    }
    while (running.late.length > 0 && now - Number(running.late[0]) > OVERRUN_WINDOW_SECONDS) {
      running.late.shift();
    }
    return running.late.length >= OVERRUN_HOPS;
  }

  private halt(): void {
    const running = this.running;
    if (running === null) {
      return;
    }
    this.running = null;
    running.node.port.postMessage({ type: 'stop' });
    running.node.port.onmessage = null;
    running.node.disconnect();
    running.session.stop();
    this.last = { ...running.session.summary(), latencySource: running.latency.source };
    this.tolerance = this.last.toleranceCents;
  }

  private estimate(): RoundTripLatency {
    const info = this.deps.input()?.info;
    const ms = info === undefined ? FALLBACK_ROUND_TRIP_MS : Math.round(info.estimatedLatencySamples / LATENCY_RATE);
    return { ms, source: 'estimated' };
  }

  private setState(state: SingingState, reason: string | null): void {
    this.current = state;
    this.deps.notify.state(state, reason);
  }
}
