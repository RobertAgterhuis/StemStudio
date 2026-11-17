/**
 * The recording and take-channel surface of the engine (contract 2.5.0): composes input, monitoring, capture,
 * recorder, capture store and take channel, and keeps the take/compare/recording gates on the mixer.
 *
 * @packageDocumentation
 */
import { recordingNotifier, type RecordingNotifier } from '../callbacks';
import type { Mixer } from '../mixer';
import { loadStems } from '../stem-loader';
import type { Transport } from '../transport';
import type {
  CalibrationResult,
  CapturedTake,
  DotNetObjectReference,
  EngineOptions,
  InputDevice,
  InputInfo,
  LoadTakeRequest,
  MonitoringSettings,
  OpenInputRequest,
  RecordOptions,
  RecordingPart,
  RecordingSupport,
  TakeCompareMode,
  TakeSource,
} from '../types';
import { CaptureReceiver } from './capture';
import { takeGates, type RecordingGating } from './compare';
import { browserEnvironment, type MediaEnvironment } from './environment';
import { InputPort } from './input';
import { Monitor } from './monitoring';
import { notImplemented, type RecordingSurface } from './pending';
import { Recorder, type RecorderPractice } from './recorder';
import { checkSupport } from './support';
import { TakeChannel, sliceTake } from './take-channel';
import { CaptureStore } from './takes';

/** Largest playback-only timing adjustment in either direction (samples, 200 ms). */
export const MAX_ADJUSTMENT_SAMPLES = 8820;
/** Message of the calls refused while a session runs. */
export const LOCKED_MESSAGE = 'Stop the recording first';

/** Engine parts of the recording. */
export interface RecordingHost {
  readonly context: AudioContext;
  readonly transport: Transport;
  readonly mixer: Mixer;
  readonly practice: RecorderPractice;
  readonly fetch: typeof fetch;
  readonly ref: DotNetObjectReference;
  readonly options: EngineOptions;
}

/**
 * @param samples - Requested adjustment.
 * @returns The adjustment when it is an integer in -8820..8820.
 * @throws Error `Invalid take adjustment: <value> (-8820..8820 samples)`.
 */
export function validateAdjustment(samples: number): number {
  if (!Number.isInteger(samples) || Math.abs(samples) > MAX_ADJUSTMENT_SAMPLES) {
    throw new Error(`Invalid take adjustment: ${String(samples)} (-8820..8820 samples)`);
  }
  return samples;
}

function integerIn(value: number, min: number, max: number): boolean {
  return Number.isInteger(value) && value >= min && value <= max;
}

/** Longest take accepted by `loadTake` (samples, the 30-minute session limit). */
export const MAX_TAKE_SAMPLES = 1800 * 44100;

/**
 * @param source - A saved take's span (from the backend, untrusted shape).
 * @throws Error `Invalid take source: <field> ...` for a negative or fractional start, a length outside
 *   1..{@link MAX_TAKE_SAMPLES} or a channel count other than 1 or 2.
 */
export function validateTakeSlice(source: { readonly startSample: number; readonly lengthSamples: number; readonly channels: number }): void {
  const checks: readonly [string, boolean][] = [
    ['startSample', integerIn(source.startSample, 0, Number.MAX_SAFE_INTEGER)],
    ['lengthSamples', integerIn(source.lengthSamples, 1, MAX_TAKE_SAMPLES)],
    ['channels', integerIn(source.channels, 1, 2)],
  ];
  const failed = checks.find(([, ok]) => !ok);
  if (failed !== undefined) {
    throw new Error(`Invalid take source: ${failed[0]}`);
  }
}

/** Recording surface of one engine. */
export class Recording implements RecordingSurface {
  private readonly env: MediaEnvironment;
  private readonly notify: RecordingNotifier;
  private readonly input: InputPort;
  private readonly monitor: Monitor;
  private readonly capture: CaptureReceiver;
  private readonly store = new CaptureStore();
  private readonly recorder: Recorder;
  private readonly take: TakeChannel;
  private part: RecordingPart = 'none';
  private mode: TakeCompareMode = 'both';
  private gating: RecordingGating | null = null;

  /** @param host - Engine parts. */
  public constructor(private readonly host: RecordingHost) {
    const { context, transport, mixer, options } = host;
    this.env = browserEnvironment(options);
    this.notify = recordingNotifier(host.ref);
    this.input = new InputPort({ context, env: this.env, onLost: this.lost, onDevicesChanged: this.devicesChanged });
    this.monitor = new Monitor(context);
    this.capture = new CaptureReceiver({ context, env: this.env, ...(options.captureWorkletUrl === undefined ? {} : { workletUrl: options.captureWorkletUrl }), onLimit: this.limited, onError: this.failed });
    this.recorder = this.createRecorder();
    this.take = new TakeChannel({ context, transport, output: () => mixer.input('take') });
    transport.addListener(this.recorder);
    transport.addListener(this.take);
  }

  /** Whether a session runs: seek, loop, speed, transpose, track and input changes are refused. */
  public get busy(): boolean {
    return this.recorder.busy;
  }

  /** Meter tap of the open input. */
  public get inputAnalyser(): AnalyserNode | undefined {
    return this.input.analyser;
  }

  /** @throws Error `Stop the recording first` while a session runs. */
  public assertIdle(): void {
    if (this.busy) {
      throw new Error(LOCKED_MESSAGE);
    }
  }

  /** {@inheritDoc AudioEngine.recordingSupport} */
  public recordingSupport(): Promise<RecordingSupport> {
    return checkSupport(this.env, this.host.context, this.input.rateMismatch);
  }

  /** {@inheritDoc AudioEngine.listInputDevices} */
  public listInputDevices(): Promise<InputDevice[]> {
    return this.input.listDevices();
  }

  /** {@inheritDoc AudioEngine.openInput} */
  public async openInput(request: OpenInputRequest): Promise<InputInfo> {
    this.assertIdle();
    const info = await this.input.open(request);
    try {
      await this.capture.connect(this.selected(), this.input.channels);
    } catch (error) {
      this.input.close();
      throw error;
    }
    this.monitor.attach(this.selected());
    this.recorder.inputOpened();
    return info;
  }

  /** {@inheritDoc AudioEngine.closeInput} */
  public closeInput(): void {
    this.assertIdle();
    this.release();
    this.recorder.inputClosed();
  }

  /** {@inheritDoc AudioEngine.setMonitoring} */
  public setMonitoring(settings: MonitoringSettings): void {
    this.monitor.set(settings);
  }

  /** {@inheritDoc AudioEngine.calibrateLatency} (015 US2, T046). */
  public readonly calibrateLatency: RecordingSurface['calibrateLatency'] = (): Promise<CalibrationResult> => Promise.reject(notImplemented('calibrateLatency'));

  /** {@inheritDoc AudioEngine.startRecording} */
  public startRecording(options: RecordOptions): Promise<void> {
    return this.recorder.start(options);
  }

  /** {@inheritDoc AudioEngine.stopRecording} */
  public stopRecording(): Promise<CapturedTake[]> {
    return this.recorder.stop();
  }

  /** {@inheritDoc AudioEngine.captureWav} */
  public captureWav(captureKey: string): Blob {
    return this.store.wav(captureKey);
  }

  /** {@inheritDoc AudioEngine.releaseCapture} */
  public releaseCapture(captureKey: string): void {
    this.store.release(captureKey);
  }

  /** {@inheritDoc AudioEngine.loadTake} */
  public async loadTake(request: LoadTakeRequest): Promise<void> {
    const adjustment = validateAdjustment(request.adjustmentSamples ?? 0);
    const { buffer, startSample } = await this.takeAudio(request.source);
    this.host.mixer.ensureTake();
    this.part = request.part;
    this.take.load(buffer, startSample + adjustment);
    this.applyGates();
  }

  /** {@inheritDoc AudioEngine.unloadTake} */
  public unloadTake(): void {
    this.take.unload();
    this.mode = 'both';
    this.applyGates();
  }

  /** {@inheritDoc AudioEngine.setTakeAdjustment} (015 US7, T078). */
  public readonly setTakeAdjustment: RecordingSurface['setTakeAdjustment'] = (): void => {
    throw notImplemented('setTakeAdjustment');
  };

  /** {@inheritDoc AudioEngine.setTakeCompare} (015 US4, T060). */
  public readonly setTakeCompare: RecordingSurface['setTakeCompare'] = (): void => {
    throw notImplemented('setTakeCompare');
  };

  /** A new track was loaded: the take belongs to the old one. */
  public onTrackLoaded(): void {
    this.take.unload();
    this.part = 'none';
    this.mode = 'both';
    this.applyGates();
  }

  /** Stops everything and releases the input and every capture. */
  public dispose(): void {
    this.recorder.dispose();
    this.take.dispose();
    this.release();
    this.input.dispose();
    this.store.clear();
  }

  private createRecorder(): Recorder {
    const { context, transport, practice } = this.host;
    return new Recorder({
      context,
      transport,
      practice,
      capture: this.capture,
      store: this.store,
      input: this.input,
      events: this.notify,
      gating: (gating) => {
        this.gating = gating;
        this.applyGates();
      },
    });
  }

  private selected(): AudioNode {
    const node = this.input.node;
    if (node === null) {
      throw new Error('notFound: The input closed while opening. Try again.');
    }
    return node;
  }

  private release(): void {
    this.capture.disconnect();
    this.monitor.attach(null);
    this.input.close();
  }

  private async takeAudio(source: TakeSource): Promise<{ buffer: AudioBuffer; startSample: number }> {
    if ('captureKey' in source) {
      return { buffer: this.captureBuffer(source.captureKey), startSample: this.store.startOf(source.captureKey) };
    }
    validateTakeSlice(source);
    const { context } = this.host;
    const decoded = await loadStems(context, this.host.fetch, [{ name: 'take', url: source.url }]);
    return { buffer: sliceTake(context, [...decoded.values()][0] ?? context.createBuffer(1, 1, context.sampleRate), source), startSample: source.startSample };
  }

  /** The local capture as an `AudioBuffer`. */
  private captureBuffer(captureKey: string): AudioBuffer {
    const { context } = this.host;
    const samples = this.store.samples(captureKey);
    if (samples === undefined) {
      throw new Error(`Unknown capture "${captureKey}"`);
    }
    const buffer = context.createBuffer(samples.length, Math.max(1, samples[0]?.length ?? 1), context.sampleRate);
    samples.forEach((channel, c) => {
      buffer.getChannelData(c).set(channel);
    });
    return buffer;
  }

  private applyGates(): void {
    const neutral = this.mode === 'both' && this.gating === null;
    const gate = neutral ? null : takeGates({ mode: this.mode, part: this.part, recording: this.gating });
    this.host.mixer.setExtraGate(gate, this.host.context.currentTime);
  }

  private readonly lost = (reason: string): void => {
    void this.recorder.inputLost();
    this.release();
    this.notify.inputLost(reason);
  };

  private readonly devicesChanged = (): void => {
    this.notify.devicesChanged();
  };

  private readonly limited = (): void => {
    void this.recorder.stop();
  };

  private readonly failed = (reason: string): void => {
    if (this.input.isOpen) {
      this.lost(reason);
    }
  };
}
