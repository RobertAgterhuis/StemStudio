import { vi } from 'vitest';
import type { MediaEnvironment } from '../../src/recording/environment';
import { MockAudioNode, type MockAudioContext } from './audio-context';
import { MockAudioWorkletNode, WorkletContext } from './worklet-node';

/** Node double that also records the output index of each connection (splitters). */
export class MockRoutedNode extends MockAudioNode {
  public readonly routes: { readonly node: MockAudioNode; readonly output: number }[] = [];

  public override connect<T extends MockAudioNode>(destination: T, output = 0): T {
    this.routes.push({ node: destination, output });
    return super.connect(destination);
  }
}

/** `MediaStreamTrack` double: settings, `stop()`, `ended` listeners. */
export class FakeTrack extends EventTarget {
  public stopped = false;
  public readonly kind = 'audio';

  public constructor(
    public readonly label: string,
    private readonly settings: MediaTrackSettings,
  ) {
    super();
  }

  public getSettings(): MediaTrackSettings {
    return { ...this.settings };
  }

  public stop(): void {
    this.stopped = true;
  }

  /** @returns A live copy with the same label and settings (stopping one never stops the other). */
  public clone(): FakeTrack {
    return new FakeTrack(this.label, this.settings);
  }

  /** Simulates an unplugged device. */
  public end(): void {
    this.dispatchEvent(new Event('ended'));
  }
}

/** `MediaStream` double with audio tracks. */
export class FakeStream {
  public constructor(public readonly tracks: FakeTrack[]) {}

  public getAudioTracks(): FakeTrack[] {
    return this.tracks;
  }

  public getTracks(): FakeTrack[] {
    return this.tracks;
  }

  /** @returns A new stream of cloned tracks. */
  public clone(): FakeStream {
    return new FakeStream(this.tracks.map((track) => track.clone()));
  }
}

/** One input device of {@link FakeMediaDevices}. */
export interface FakeDevice {
  readonly deviceId: string;
  readonly label: string;
  readonly channelCount: 1 | 2;
  readonly sampleRate?: number;
  readonly latency?: number;
}

/** Track settings of a fake device (`latency` is a Chromium extension). */
function settingsOf(device: FakeDevice): MediaTrackSettings {
  const { channelCount, deviceId, sampleRate, latency } = device;
  return { channelCount, deviceId, ...(sampleRate === undefined ? {} : { sampleRate }), ...(latency === undefined ? {} : { latency }) };
}

/** `navigator.mediaDevices` double. */
export class FakeMediaDevices extends EventTarget {
  public readonly devices: FakeDevice[] = [
    { deviceId: 'usb', label: 'USB Interface', channelCount: 2, sampleRate: 48000, latency: 0.01 },
    { deviceId: 'mic', label: 'Laptop Mic', channelCount: 1 },
  ];
  public readonly requests: MediaStreamConstraints[] = [];
  public readonly streams: FakeStream[] = [];
  public granted = false;
  /** When set, `getUserMedia` rejects with a DOMException of this name. */
  public failWith: string | null = null;

  public getUserMedia = (constraints: MediaStreamConstraints): Promise<MediaStream> => {
    this.requests.push(constraints);
    if (this.failWith !== null) {
      return Promise.reject(new DOMException('fake failure', this.failWith));
    }
    const device = this.pick(constraints);
    this.granted = true;
    const stream = new FakeStream([new FakeTrack(device.label, settingsOf(device))]);
    this.streams.push(stream);
    return Promise.resolve(stream as unknown as MediaStream);
  };

  private pick(constraints: MediaStreamConstraints): FakeDevice {
    const audio = constraints.audio as MediaTrackConstraints;
    const wanted = (audio.deviceId as { exact?: string } | undefined)?.exact;
    return this.devices.find((d) => d.deviceId === wanted) ?? this.devices[0] ?? { deviceId: 'default', label: 'Default', channelCount: 1 };
  }

  public enumerateDevices = (): Promise<MediaDeviceInfo[]> =>
    Promise.resolve([
      ...this.devices.map((d) => ({ kind: 'audioinput', deviceId: d.deviceId, label: this.granted ? d.label : '', groupId: `g-${d.deviceId}` })),
      { kind: 'audiooutput', deviceId: 'speakers', label: 'Speakers', groupId: 'g-out' },
    ] as unknown as MediaDeviceInfo[]);

  /** The last opened stream's track. */
  public get track(): FakeTrack | undefined {
    return this.streams.at(-1)?.tracks[0];
  }
}

/** Worklet context plus the media nodes the recording uses. */
export class RecordingContext extends WorkletContext {
  public readonly splitters: MockRoutedNode[] = [];
  public readonly mediaSources: (MockRoutedNode & { readonly stream: unknown })[] = [];
  public readonly mediaDestinations: (MockAudioNode & { readonly stream: FakeStream })[] = [];
  /** When set, `createMediaStreamSource` throws a DOMException of this name (Firefox rate mismatch). */
  public sourceError: string | null = null;

  public createChannelSplitter(): MockRoutedNode {
    const node = new MockRoutedNode(this, 'splitter');
    this.splitters.push(node);
    return node;
  }

  public createMediaStreamSource(stream: unknown): MockRoutedNode {
    if (this.sourceError !== null) {
      throw new DOMException('rate', this.sourceError);
    }
    const node = Object.assign(new MockRoutedNode(this, 'media-source'), { stream });
    this.mediaSources.push(node);
    return node;
  }

  public createMediaStreamDestination(): MockAudioNode & { readonly stream: FakeStream } {
    const stream = new FakeStream([new FakeTrack('loopback', { channelCount: 2 })]);
    const node = Object.assign(new MockAudioNode(this as MockAudioContext, 'media-destination'), { stream });
    this.mediaDestinations.push(node);
    return node;
  }

  /** The capture worklet nodes. */
  public captureNodes(): MockAudioWorkletNode[] {
    return this.workletNodes.filter((n) => n.name === 'stemstudio-capture');
  }
}

/** A test {@link MediaEnvironment} over the fakes. */
export function fakeEnvironment(
  devices: FakeMediaDevices | null = new FakeMediaDevices(),
  overrides: Partial<MediaEnvironment> = {},
): MediaEnvironment {
  return {
    secure: true,
    mediaDevices: devices ?? undefined,
    permissions: { query: () => Promise.resolve({ state: devices?.granted === true ? 'granted' : 'prompt' }) },
    createWorkletNode: (context, name, options) => (context as unknown as RecordingContext).createWorkletNode(name, options) as unknown as AudioWorkletNode,
    ...overrides,
  };
}

/** Stubs a secure page whose `new AudioWorkletNode(...)` creates the {@link RecordingContext}'s mock nodes. */
export function stubSecureWorklets(): void {
  vi.stubGlobal('isSecureContext', true);
  vi.stubGlobal('AudioWorkletNode', function AudioWorkletNode(context: RecordingContext, name: string, options: AudioWorkletNodeOptions) {
    return context.createWorkletNode(name, options);
  });
}
