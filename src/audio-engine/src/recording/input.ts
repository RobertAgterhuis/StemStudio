/**
 * The recording input (contract 2.5.0, research R-02, FR-001..FR-003): device list, `getUserMedia` with the raw-signal
 * constraints, channel selection per mode, the input meter tap, device loss and `devicechange`.
 *
 * Graph: `MediaStreamSource -> [ChannelSplitter output 0|1 for mono-1|mono-2] -> Gain(1)` (the selected-channel node
 * that feeds the capture worklet, the input meter and monitoring).
 *
 * @packageDocumentation
 */
import type { InputChannelMode, InputDevice, InputInfo, OpenInputRequest } from '../types';
import type { MediaEnvironment } from './environment';

/** Context rate of the engine. */
const RATE = 44100;
/** Device id reported for the E2E loopback input. */
const LOOPBACK_ID = 'loopback';

/** Construction parameters of {@link InputPort}. */
export interface InputPortOptions {
  readonly context: AudioContext;
  readonly env: MediaEnvironment;
  /** The open device was unplugged or its track ended. */
  readonly onLost: (reason: string) => void;
  /** A `devicechange` event. */
  readonly onDevicesChanged: () => void;
  /** E2E only: the engine's own output as a stream (research R-13), used instead of a microphone. */
  readonly loopback?: () => MediaStream;
}

/** The nodes and stream of the open input. */
interface OpenInput {
  readonly stream: MediaStream;
  readonly track: MediaStreamTrack;
  readonly source: MediaStreamAudioSourceNode;
  readonly splitter: ChannelSplitterNode | null;
  readonly selected: GainNode;
  readonly analyser: AnalyserNode;
  readonly info: InputInfo;
}

/**
 * @param deviceId - Device, or null for the browser default.
 * @returns The `getUserMedia` constraints of research R-02 (no processing, lowest latency).
 */
export function inputConstraints(deviceId: string | null): MediaStreamConstraints {
  const audio: Record<string, unknown> = {
    deviceId: deviceId === null ? undefined : { exact: deviceId },
    channelCount: { ideal: 2 },
    echoCancellation: false,
    noiseSuppression: false,
    autoGainControl: false,
    voiceIsolation: false,
    latency: { ideal: 0 },
  };
  return { audio, video: false };
}

const ERROR_CODES: Readonly<Record<string, string>> = {
  NotAllowedError: 'permissionDenied: Microphone access was denied. Allow it in the browser site settings and try again.',
  SecurityError: 'permissionDenied: Microphone access was denied. Allow it in the browser site settings and try again.',
  NotFoundError: 'notFound: The selected input was not found. Connect it or choose another input.',
  OverconstrainedError: 'notFound: The selected input was not found. Connect it or choose another input.',
  NotReadableError: 'busy: The input is in use by another program or cannot be opened. Close the other program and try again.',
  AbortError: 'busy: The input is in use by another program or cannot be opened. Close the other program and try again.',
};

/** Message of a rate mismatch (`NotSupportedError` from `createMediaStreamSource`, Firefox). */
const RATE_MISMATCH = 'unsupported: This browser cannot record from an input whose sample rate differs from 44.1 kHz.';

/**
 * @param error - A `getUserMedia` rejection.
 * @returns An `Error` whose message starts with the contract code (`permissionDenied:`, `notFound:`, `busy:`,
 *   `unsupported:`).
 */
export function inputError(error: unknown): Error {
  return new Error(ERROR_CODES[errorName(error)] ?? `unsupported: The input cannot be opened (${String(error)}).`, { cause: error });
}

/** @returns The `name` of an Error or DOMException (not every DOMException is an `Error` instance), else ''. */
function errorName(error: unknown): string {
  const name = (error as { readonly name?: unknown } | null)?.name;
  return typeof name === 'string' ? name : '';
}

/**
 * @param context - The engine's AudioContext.
 * @returns `baseLatency + outputLatency` (s); missing values count as 0.
 */
export function outputLatencySeconds(context: AudioContext): number {
  const optional: Partial<Pick<AudioContext, 'baseLatency' | 'outputLatency'>> = context;
  return (optional.baseLatency ?? 0) + (optional.outputLatency ?? 0);
}

function deviceIdOf(settings: MediaTrackSettings, request: OpenInputRequest): string {
  return settings.deviceId ?? request.deviceId ?? '';
}

function channelsOf(track: MediaStreamTrack): 1 | 2 {
  return (track.getSettings().channelCount ?? 1) >= 2 ? 2 : 1;
}

function stopAll(stream: MediaStream): void {
  stream.getTracks().forEach((track) => {
    track.stop();
  });
}

/** Opens, describes and closes the one recording input. */
export class InputPort {
  private current: OpenInput | null = null;
  private mismatch = false;

  /** @param options - Context, environment and callbacks. */
  public constructor(private readonly options: InputPortOptions) {
    options.env.mediaDevices?.addEventListener('devicechange', this.devicesChanged);
  }

  /** Whether an input is open. */
  public get isOpen(): boolean {
    return this.current !== null;
  }

  /** The open input, or null. */
  public get info(): InputInfo | null {
    return this.current?.info ?? null;
  }

  /** Selected-channel node of the open input, or null. */
  public get node(): AudioNode | null {
    return this.current?.selected ?? null;
  }

  /** Input meter tap, or undefined while closed. */
  public get analyser(): AnalyserNode | undefined {
    return this.current?.analyser;
  }

  /** Channels of the selected mode (1 for `mono-*`, 2 for `stereo`). */
  public get channels(): 1 | 2 {
    return this.current?.info.mode === 'stereo' ? 2 : 1;
  }

  /** Whether the browser refused a device rate (Firefox, `rateMismatch`). */
  public get rateMismatch(): boolean {
    return this.mismatch;
  }

  /** @returns Every audio input; labels are empty until permission is granted. */
  public async listDevices(): Promise<InputDevice[]> {
    const devices = (await this.options.env.mediaDevices?.enumerateDevices()) ?? [];
    return devices.filter((d) => d.kind === 'audioinput').map((d) => ({ deviceId: d.deviceId, label: d.label, groupId: d.groupId }));
  }

  /**
   * Opens (or replaces) the input.
   *
   * @param request - Device and channel mode.
   * @returns The open input.
   */
  public async open(request: OpenInputRequest): Promise<InputInfo> {
    this.close();
    const stream = await this.requestStream(request.deviceId);
    try {
      this.current = this.build(stream, request);
    } catch (error) {
      stopAll(stream);
      throw error;
    }
    this.current.track.addEventListener('ended', this.ended);
    return this.current.info;
  }

  /** Stops every track and disconnects the nodes (no-op when closed). */
  public close(): void {
    const open = this.current;
    if (open === null) {
      return;
    }
    this.current = null;
    open.track.removeEventListener('ended', this.ended);
    stopAll(open.stream);
    [open.source, open.splitter, open.selected, open.analyser].forEach((node) => node?.disconnect());
  }

  /** Closes the input and stops following `devicechange`. */
  public dispose(): void {
    this.close();
    this.options.env.mediaDevices?.removeEventListener('devicechange', this.devicesChanged);
  }

  private async requestStream(deviceId: string | null): Promise<MediaStream> {
    if (this.options.loopback !== undefined) {
      return this.options.loopback();
    }
    const devices = this.options.env.mediaDevices;
    if (devices === undefined) {
      throw new Error('unsupported: This browser gives no access to audio inputs.');
    }
    try {
      return await devices.getUserMedia(inputConstraints(deviceId));
    } catch (error) {
      throw inputError(error);
    }
  }

  private build(stream: MediaStream, request: OpenInputRequest): OpenInput {
    const [track] = stream.getAudioTracks();
    if (track === undefined) {
      throw new Error('notFound: The input delivers no audio. Choose another input.');
    }
    const channelCount = channelsOf(track);
    if (request.mode !== 'mono-1' && channelCount < 2) {
      throw new Error('modeUnavailable: This input has one channel. Choose mono (input 1).');
    }
    const source = this.createSource(stream);
    const { splitter, selected } = this.select(source, request.mode);
    const analyser = this.options.context.createAnalyser();
    selected.connect(analyser);
    return { stream, track, source, splitter, selected, analyser, info: this.describe(track, request, channelCount) };
  }

  private createSource(stream: MediaStream): MediaStreamAudioSourceNode {
    try {
      return this.options.context.createMediaStreamSource(stream);
    } catch (error) {
      if (errorName(error) === 'NotSupportedError') {
        this.mismatch = true;
        throw new Error(RATE_MISMATCH, { cause: error });
      }
      throw inputError(error);
    }
  }

  private select(source: AudioNode, mode: InputChannelMode): { splitter: ChannelSplitterNode | null; selected: GainNode } {
    const { context } = this.options;
    const selected = context.createGain();
    if (mode === 'stereo') {
      source.connect(selected);
      return { splitter: null, selected };
    }
    const splitter = context.createChannelSplitter(2);
    source.connect(splitter);
    splitter.connect(selected, mode === 'mono-2' ? 1 : 0);
    return { splitter, selected };
  }

  private describe(track: MediaStreamTrack, request: OpenInputRequest, channelCount: 1 | 2): InputInfo {
    const settings: MediaTrackSettings & { latency?: number } = track.getSettings();
    const output = outputLatencySeconds(this.options.context);
    return {
      deviceId: this.options.loopback === undefined ? deviceIdOf(settings, request) : LOOPBACK_ID,
      label: track.label,
      channelCount,
      mode: request.mode,
      deviceSampleRate: settings.sampleRate ?? null,
      estimatedLatencySamples: Math.round((output + (settings.latency ?? 0)) * RATE),
      outputLatencySamples: Math.round(output * RATE),
    };
  }

  private readonly ended = (): void => {
    if (this.current !== null) {
      this.close();
      this.options.onLost('ended');
    }
  };

  private readonly devicesChanged = (): void => {
    this.options.onDevicesChanged();
  };
}
