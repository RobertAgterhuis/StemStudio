/**
 * The recording state machine (contract 2.5.0, data-model 3.1):
 * `closed -> open -> countIn -> recording -> finishing -> open` (or `closed` after a lost input).
 *
 * Record starts the capture, then the transport from the current position with 012's count-in (bars of the session).
 * While a session runs the transport hands its heard anchors to the session's segment log. Stop (the user, the end
 * of the song, a pause, the pass or time limit, a lost input) waits until the capture covers `stopFrame + L` (plus
 * one batch), then maps the capture to takes. Timers here only wake the progress report and the tail wait; every
 * take boundary comes from context frames.
 *
 * @packageDocumentation
 */
import type { Transport, TransportListener } from '../transport';
import type { CapturedTake, InputInfo, PlaybackState, PracticeStatus, RecordOptions, RecordingProgress, RecordingState } from '../types';
import type { CaptureReceiver } from './capture';
import { CAPTURE_BATCH_FRAMES } from './capture-worklet';
import type { RecordingGating } from './compare';
import { mapCaptureToTakes, passAt, type LoopSpan, type TransportSegment } from './mapping';

/** A loop pass and the heard frame it started at. */
type PassPosition = NonNullable<ReturnType<typeof passAt>>;
import { assertCanRecord, validateRecordOptions, type ValidRecordOptions } from './record-options';
import { SegmentLog } from './segments';
import { describeTake, type CaptureStore, type SessionInfo } from './takes';

export { validateRecordOptions } from './record-options';

/** Interval of `OnRecordingProgress` (4 Hz). UI feedback only. */
export const PROGRESS_INTERVAL_MS = 250;
/** Poll interval of the tail wait (ms). */
const TAIL_POLL_MS = 20;
/** Extra wall time the tail wait allows before it gives up (ms). */
const TAIL_SLACK_MS = 1000;

/** The practice controller as the recorder uses it. */
export interface RecorderPractice {
  /** Current practice status (speed, transpose, trainer, count-in). */
  status(): PracticeStatus;
  /** Count-in bars for the session, or null to restore the stored setting. */
  overrideCountIn(bars: number | null): void;
  /** Starts the transport with the count-in. */
  play(): Promise<void>;
}

/** The input as the recorder uses it. */
export interface RecorderInput {
  readonly isOpen: boolean;
  readonly info: InputInfo | null;
  readonly channels: 1 | 2;
}

/** Callbacks to .NET. */
export interface RecorderEvents {
  /** `OnRecordingStateChanged`. */
  state(state: RecordingState): void;
  /** `OnRecordingProgress`. */
  progress(progress: RecordingProgress): void;
  /** `OnTakesCaptured`. */
  takes(takes: CapturedTake[]): void;
}

/** Engine parts of the recorder. */
export interface RecorderHost {
  readonly context: AudioContext;
  readonly transport: Transport;
  readonly capture: CaptureReceiver;
  readonly store: CaptureStore;
  readonly input: RecorderInput;
  readonly practice: RecorderPractice;
  /** Sets (or clears) the recording gates (take silent, part muted). */
  readonly gating: (gating: RecordingGating | null) => void;
  readonly events: RecorderEvents;
}

/** One Record press. */
interface Session {
  readonly options: ValidRecordOptions;
  readonly info: SessionInfo;
  readonly loop: LoopSpan | null;
  readonly trackSamples: number;
  readonly log: SegmentLog;
}

/** How a session ends. */
interface Ending {
  readonly interrupted: boolean;
  /** Heard frame of the end; default: the heard frame now. */
  readonly stopFrame?: number;
}

/** @returns A random UUID (crypto.randomUUID, or getRandomValues where it is missing). */
export function newUuid(): string {
  const crypto = globalThis.crypto as Crypto & { randomUUID?: () => string };
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-4${hex.slice(13, 16)}-a${hex.slice(17, 20)}-${hex.slice(20, 32)}`;
}

/** @returns Heard frame of the first segment (the music start), Infinity before it was logged. */
function firstHeardFrame(segments: readonly TransportSegment[]): number {
  return segments[0]?.whenFrame ?? Infinity;
}

/** Runs recording sessions over the open input. */
export class Recorder implements TransportListener {
  private current: RecordingState = 'closed';
  private session: Session | null = null;
  private ending: Promise<CapturedTake[]> | null = null;
  private timer: ReturnType<typeof setInterval> | undefined;

  /** @param host - Engine parts. */
  public constructor(private readonly host: RecorderHost) {}

  /** Current state. */
  public get state(): RecordingState {
    return this.current;
  }

  /** Whether a session runs (count-in, recording or finishing): transport controls are locked. */
  public get busy(): boolean {
    return this.session !== null;
  }

  /** The input opened. */
  public inputOpened(): void {
    if (!this.busy) {
      this.setState('open');
    }
  }

  /** The input closed. */
  public inputClosed(): void {
    if (!this.busy) {
      this.setState('closed');
    }
  }

  /**
   * Starts a session (`startRecording`).
   *
   * @param input - Record options.
   */
  public async start(input: RecordOptions): Promise<void> {
    const options = validateRecordOptions(input);
    const { transport, capture, practice } = this.host;
    assertCanRecord({ running: this.busy, inputOpen: this.current === 'open' && this.host.input.isOpen, trackLoaded: transport.duration > 0, practice: practice.status() });
    if (transport.state === 'playing') {
      transport.pause();
    }
    const session = this.open(options);
    this.session = session;
    transport.setAnchorSink(session.log.record);
    capture.start(options.maxSeconds);
    this.host.gating({ part: options.part, muteOriginal: options.muteOriginal });
    practice.overrideCountIn(options.countInBars);
    await practice.play();
    this.setState(practice.status().countIn.active ? 'countIn' : 'recording');
    this.timer = setInterval(this.tick, PROGRESS_INTERVAL_MS);
  }

  /** @returns The takes of the session (`stopRecording`); empty when none runs. */
  public stop(): Promise<CapturedTake[]> {
    return this.end({ interrupted: false });
  }

  /** @returns The takes kept after the input was lost (partial); the state ends `closed`. */
  public inputLost(): Promise<CapturedTake[]> {
    if (!this.busy) {
      this.setState('closed');
    }
    return this.end({ interrupted: true });
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    const recording = this.current === 'countIn' || this.current === 'recording';
    if (state !== 'playing' && recording) {
      void this.stop();
    }
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    // Seeks are locked while recording; pause and stop end the session through onStateChanged.
  }

  /** Stops the progress timer and forgets the session (engine disposal). */
  public dispose(): void {
    clearInterval(this.timer);
    this.timer = undefined;
    this.session = null;
  }

  private open(options: ValidRecordOptions): Session {
    const { transport, context, input } = this.host;
    const rate = context.sampleRate;
    const region = transport.loop;
    const processing = Math.round(transport.timing.latency * rate);
    return {
      options,
      log: new SegmentLog(rate),
      trackSamples: Math.round(transport.duration * rate),
      loop: region.enabled ? { startSample: Math.round(region.start * rate), endSample: Math.round(region.end * rate) } : null,
      info: {
        sessionId: newUuid(),
        inputMode: input.info?.mode ?? 'mono-1',
        latencySamples: options.latencySamples + processing,
        latencyMethod: options.latencyMethod,
        recordedAt: new Date().toISOString(),
      },
    };
  }

  private readonly tick = (): void => {
    const session = this.ending === null ? this.session : null;
    if (session !== null) {
      this.report(session);
    }
  };

  /** One progress report: count-in end, pass limit, `OnRecordingProgress`. */
  private report(session: Session): void {
    const { context, practice } = this.host;
    const frame = Math.round(context.currentTime * context.sampleRate);
    const segments = session.log.segments();
    const status = practice.status();
    this.leaveCountIn(status, segments, frame);
    const pass = passAt({ segments, loop: session.loop, trackSamples: session.trackSamples }, frame);
    if (!this.overLimit(session, pass)) {
      this.host.events.progress(this.progressOf(status, pass));
    }
  }

  /** Ends the session at the start of the pass after `maxPasses`. */
  private overLimit(session: Session, pass: PassPosition | null): boolean {
    if (pass === null || pass.pass <= session.options.maxPasses) {
      return false;
    }
    void this.end({ interrupted: false, stopFrame: pass.fromFrame });
    return true;
  }

  private progressOf(status: PracticeStatus, pass: PassPosition | null): RecordingProgress {
    const { context, capture } = this.host;
    const countInBeatsLeft = status.countIn.active ? status.countIn.beatsRemaining : null;
    return { elapsedSeconds: capture.capturedFrames / context.sampleRate, passIndex: pass?.pass ?? null, countInBeatsLeft };
  }

  /** `countIn -> recording` once the count-in is over and the music start is heard. */
  private leaveCountIn(status: PracticeStatus, segments: readonly TransportSegment[], frame: number): void {
    const waiting = this.current === 'countIn' && !status.countIn.active;
    if (waiting && frame >= firstHeardFrame(segments)) {
      this.setState('recording');
    }
  }

  private end(ending: Ending): Promise<CapturedTake[]> {
    const session = this.session;
    if (this.ending !== null) {
      return this.ending;
    }
    if (session === null) {
      return Promise.resolve([]);
    }
    this.ending = this.finish(session, ending).finally(() => {
      this.ending = null;
    });
    return this.ending;
  }

  private async finish(session: Session, ending: Ending): Promise<CapturedTake[]> {
    const { transport, context, capture } = this.host;
    clearInterval(this.timer);
    this.setState('finishing');
    const stopFrame = ending.stopFrame ?? Math.round((context.currentTime + transport.timing.latency) * context.sampleRate);
    if (transport.state === 'playing') {
      transport.pause();
    }
    await this.awaitTail(stopFrame + session.options.latencySamples);
    capture.stop();
    const takes = this.collect(session, { ...ending, stopFrame });
    this.session = null;
    this.setState(this.host.input.isOpen ? 'open' : 'closed');
    this.host.events.takes(takes);
    return takes;
  }

  /** Waits until the capture reaches `frame` (or gives up after the expected time plus a second). */
  private async awaitTail(frame: number): Promise<void> {
    const { context, capture } = this.host;
    const expectedMs = ((frame - context.currentTime * context.sampleRate + CAPTURE_BATCH_FRAMES) / context.sampleRate) * 1000;
    const polls = Math.ceil((Math.max(0, expectedMs) + TAIL_SLACK_MS) / TAIL_POLL_MS);
    for (let i = 0; i < polls && capture.endFrame < frame; i++) {
      await new Promise((resolve) => setTimeout(resolve, TAIL_POLL_MS));
    }
  }

  /** Restores the transport hooks and gates, maps the capture and stores the takes' audio. */
  private collect(session: Session, ending: Required<Ending>): CapturedTake[] {
    const { transport, capture, practice, store } = this.host;
    transport.setAnchorSink(null);
    practice.overrideCountIn(null);
    this.host.gating(null);
    const mapped = mapCaptureToTakes({
      capture: capture.take(),
      segments: session.log.segments(),
      loop: session.loop,
      compensation: session.options.latencySamples,
      stopFrame: ending.stopFrame,
      trackSamples: session.trackSamples,
      interrupted: ending.interrupted,
    });
    return mapped.map((take) => {
      const key = newUuid();
      store.put(key, take.channels);
      return describeTake(take, session.info, key);
    });
  }

  private setState(state: RecordingState): void {
    if (state !== this.current) {
      this.current = state;
      this.host.events.state(state);
    }
  }
}
