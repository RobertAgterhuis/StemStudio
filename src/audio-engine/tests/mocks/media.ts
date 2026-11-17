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
}

/** One input device of {@link FakeMediaDevices}. */
export interface FakeDevice {
  readonly deviceId: string;
  readonly label: string;
  readonly channelCount: 1 | 2;
  readonly sampleRate?: number;
  readonly latency?: number;
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
    const audio = constraints.audio as MediaTrackConstraints;
    const wanted = (audio.deviceId as { exact?: string } | undefined)?.exact;
    const device = this.devices.find((d) => d.deviceId === wanted) ?? this.devices[0];
    if (device === undefined) {
      return Promise.reject(new DOMException('none', 'NotFoundError'));
    }
    this.granted = true;
    const { channelCount, sampleRate, latency, deviceId } = device;
    const stream = new FakeStream([new FakeTrack(device.label, { channelCount, sampleRate, latency, deviceId })]);
    this.streams.push(stream);
    return Promise.resolve(stream as unknown as MediaStream);
  };

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
  devices: FakeMediaDevices | undefined = new FakeMediaDevices(),
  overrides: Partial<MediaEnvironment> = {},
): MediaEnvironment {
  return {
    secure: true,
    mediaDevices: devices as unknown as MediaEnvironment['mediaDevices'],
    permissions: { query: () => Promise.resolve({ state: devices?.granted === true ? 'granted' : 'prompt' }) },
    createWorkletNode: (context, name, options) => (context as unknown as RecordingContext).createWorkletNode(name, options) as unknown as AudioWorkletNode,
    ...overrides,
  };
}
