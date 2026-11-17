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
  AudioEngine,
  CalibrationOptions,
  CalibrationResult,
  ChannelId,
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
import { runCalibration } from './calibrate-run';
import { validateCalibrationOptions } from './calibration';
import { CaptureReceiver } from './capture';
import { takeGates, type RecordingGating } from './compare';
import { browserEnvironment, type MediaEnvironment } from './environment';
import { InputPort, outputLatencySeconds } from './input';
import { Monitor } from './monitoring';
import { Recorder, type RecorderPractice } from './recorder';
import { checkSupport } from './support';
import { TakeChannel, sliceTake } from './take-channel';
import { CaptureStore, shiftedWav } from './takes';

/** The recording and take-channel methods of {@link AudioEngine} (contract 2.5.0). */
export type RecordingSurface = Pick<
  AudioEngine,
  | 'recordingSupport'
  | 'listInputDevices'
  | 'openInput'
  | 'closeInput'
  | 'setMonitoring'
  | 'calibrateLatency'
  | 'startRecording'
  | 'stopRecording'
  | 'captureWav'
  | 'releaseCapture'
  | 'loadTake'
  | 'unloadTake'
  | 'setTakeAdjustment'
  | 'setTakeCompare'
>;

/** Largest playback-only timing adjustment in either direction (samples, 200 ms). */
export const MAX_ADJUSTMENT_SAMPLES = 8820;
/** Message of the calls refused while a session runs. */
export const LOCKED_MESSAGE = 'Stop the recording first';
/** The compare modes (research R-10). */
const COMPARE_MODES: readonly TakeCompareMode[] = ['both', 'take', 'original'];

/** Message of the calls refused while a calibration runs. */
export const CALIBRATING_MESSAGE = 'A calibration is running';

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

/** The take in the take channel. */
interface LoadedTake {
  readonly takeId: string;
  readonly buffer: AudioBuffer;
  /** Song sample of the buffer's first frame, without the adjustment. */
  readonly startSample: number;
  readonly adjustment: number;
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
  private calibrating = false;
  private loaded: LoadedTake | null = null;
  private loopback: MediaStreamAudioDestinationNode | null = null;

  /** @param host - Engine parts. */
  public constructor(private readonly host: RecordingHost) {
    const { context, transport, mixer, options } = host;
    this.env = browserEnvironment(options);
    this.notify = recordingNotifier(host.ref);
    const loopback = options.testInput === 'loopback' ? { loopback: () => this.loopbackStream() } : {};
    this.input = new InputPort({ context, env: this.env, onLost: this.lost, onDevicesChanged: this.devicesChanged, ...loopback });
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
    if (this.calibrating) {
      throw new Error(CALIBRATING_MESSAGE);
    }
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

  /** {@inheritDoc AudioEngine.calibrateLatency} */
  public async calibrateLatency(options?: CalibrationOptions): Promise<CalibrationResult> {
    this.assertCanCalibrate();
    validateCalibrationOptions(options);
    this.calibrating = true;
    this.monitor.suppress(true);
    this.applyGates();
    try {
      return await runCalibration(this.calibrationHost(), options);
    } finally {
      this.calibrating = false;
      this.monitor.suppress(false);
      this.applyGates();
    }
  }

  /** {@inheritDoc AudioEngine.startRecording} */
  public async startRecording(options: RecordOptions): Promise<void> {
    if (this.calibrating) {
      throw new Error(CALIBRATING_MESSAGE);
    }
    await this.recorder.start(options);
  }

  /** {@inheritDoc AudioEngine.stopRecording} */
  public stopRecording(): Promise<CapturedTake[]> {
    return this.recorder.stop();
  }

  /** {@inheritDoc AudioEngine.captureWav} */
  public captureWav(captureKey: string): Blob {
    const loaded = this.loaded;
    if (loaded?.takeId === captureKey) {
      return shiftedWav(loaded.buffer, loaded.startSample + loaded.adjustment);
    }
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
    this.loaded = { takeId: request.takeId, buffer, startSample, adjustment };
    this.take.load(buffer, startSample + adjustment);
    this.applyGates();
  }

  /** {@inheritDoc AudioEngine.unloadTake} */
  public unloadTake(): void {
    this.loaded = null;
    this.take.unload();
    this.mode = 'both';
    this.applyGates();
  }

  /**
   * {@inheritDoc AudioEngine.setTakeAdjustment} The adjustment replaces the current one (relative to the loaded
   * `startSample`); `captureWav(<loaded takeId>)` then returns the span re-encoded for the shifted start (US7 Apply).
   */
  public setTakeAdjustment(samples: number): void {
    const adjustment = validateAdjustment(samples);
    if (this.loaded !== null) {
      this.loaded = { ...this.loaded, adjustment };
      this.take.moveTo(this.loaded.startSample + adjustment);
    }
  }

  /** {@inheritDoc AudioEngine.setTakeCompare} */
  public setTakeCompare(mode: TakeCompareMode): void {
    if (!COMPARE_MODES.includes(mode)) {
      throw new Error(`Invalid compare mode: ${mode}`);
    }
    this.mode = mode;
    this.applyGates();
  }

  /** A new track was loaded: the take belongs to the old one. */
  public onTrackLoaded(): void {
    this.loaded = null;
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

  private assertCanCalibrate(): void {
    this.assertIdle();
    if (!this.input.isOpen) {
      throw new Error('Open an input first');
    }
    if (this.host.transport.state === 'playing') {
      throw new Error('Pause playback to calibrate');
    }
  }

  private calibrationHost(): Parameters<typeof runCalibration>[0] {
    const { context } = this.host;
    return {
      context,
      capture: this.capture,
      clickOutputs: () => (this.loopback === null ? [] : [this.loopback]),
      outputLatencySamples: () => Math.round(outputLatencySeconds(context) * context.sampleRate),
    };
  }

  /** The engine's own output as a stream (E2E `testInput: 'loopback'`, research R-13), created once. */
  private loopbackStream(): MediaStream {
    if (this.loopback === null) {
      this.loopback = this.host.context.createMediaStreamDestination();
      this.host.mixer.tap().connect(this.loopback);
    }
    return this.loopback.stream;
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
    const neutral = this.mode === 'both' && this.gating === null && !this.calibrating;
    const gate = neutral ? null : this.extraGate();
    this.host.mixer.setExtraGate(gate, this.host.context.currentTime);
  }

  private readonly lost = (reason: string): void => {
    void this.recorder.inputLost();
    this.release();
    this.notify.inputLost(reason);
  };

  /** Calibration mutes every channel; otherwise compare and recording decide. */
  private extraGate(): (id: ChannelId) => number {
    return this.calibrating ? (): number => 0 : takeGates({ mode: this.mode, part: this.part, recording: this.gating });
  }

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
