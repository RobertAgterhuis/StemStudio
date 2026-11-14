/**
 * Hand-written Web Audio test double. It records the node graph (connections), every
 * `start(when, offset)` / `stop()` call, loop settings, AudioParam automation and the times at
 * which sources were scheduled, so tests can assert sample-lock and timing without a real device.
 */

/** One recorded AudioParam automation call. */
export interface ParamCall {
  readonly method: 'setTargetAtTime' | 'setValueAtTime' | 'linearRampToValueAtTime' | 'cancelScheduledValues';
  readonly args: readonly number[];
}

/** Records automation on a parameter; `value` jumps to the target of the last call. */
export class MockAudioParam {
  public value: number;
  public readonly calls: ParamCall[] = [];

  public constructor(initial: number) {
    this.value = initial;
  }

  public setTargetAtTime(target: number, startTime: number, timeConstant: number): this {
    this.calls.push({ method: 'setTargetAtTime', args: [target, startTime, timeConstant] });
    this.value = target;
    return this;
  }

  public setValueAtTime(value: number, startTime: number): this {
    this.calls.push({ method: 'setValueAtTime', args: [value, startTime] });
    this.value = value;
    return this;
  }

  public linearRampToValueAtTime(value: number, endTime: number): this {
    this.calls.push({ method: 'linearRampToValueAtTime', args: [value, endTime] });
    this.value = value;
    return this;
  }

  public cancelScheduledValues(startTime: number): this {
    this.calls.push({ method: 'cancelScheduledValues', args: [startTime] });
    return this;
  }
}

/** Base node: records outgoing connections and disconnects. */
export class MockAudioNode {
  public readonly connections: MockAudioNode[] = [];
  public disconnected = false;

  public constructor(
    public readonly context: MockAudioContext,
    public readonly kind: string,
  ) {}

  public connect<T extends MockAudioNode>(destination: T): T {
    this.connections.push(destination);
    return destination;
  }

  public disconnect(): void {
    this.disconnected = true;
    this.connections.length = 0;
  }
}

/** Decoded audio buffer double with real channel data. */
export class MockAudioBuffer {
  private readonly channels: Float32Array[];

  public constructor(
    public readonly numberOfChannels: number,
    public readonly length: number,
    public readonly sampleRate: number,
  ) {
    this.channels = Array.from({ length: numberOfChannels }, () => new Float32Array(length));
  }

  public get duration(): number {
    return this.length / this.sampleRate;
  }

  public getChannelData(channel: number): Float32Array {
    const data = this.channels[channel];
    if (data === undefined) {
      throw new RangeError(`No channel ${String(channel)}`);
    }
    return data;
  }
}

/** One `start()` call of a buffer source. */
export interface StartCall {
  readonly when: number;
  readonly offset: number | undefined;
}

/** Buffer source double: records start/stop and loop properties; `end()` fires `onended`. */
export class MockAudioBufferSourceNode extends MockAudioNode {
  public buffer: MockAudioBuffer | null = null;
  public loop = false;
  public loopStart = 0;
  public loopEnd = 0;
  public onended: (() => void) | null = null;
  public readonly startCalls: StartCall[] = [];
  public readonly stopCalls: (number | undefined)[] = [];

  public constructor(context: MockAudioContext) {
    super(context, 'source');
  }

  public start(when?: number, offset?: number): void {
    if (this.startCalls.length > 0) {
      throw new Error('InvalidStateError: start() called twice');
    }
    this.startCalls.push({ when: when ?? 0, offset });
  }

  public stop(when?: number): void {
    this.stopCalls.push(when);
  }

  /** Test helper: simulates the browser firing `ended`. */
  public end(): void {
    this.onended?.();
  }
}

/** Gain node double. */
export class MockGainNode extends MockAudioNode {
  public readonly gain = new MockAudioParam(1);

  public constructor(context: MockAudioContext) {
    super(context, 'gain');
  }
}

/** Stereo panner double. */
export class MockStereoPannerNode extends MockAudioNode {
  public readonly pan = new MockAudioParam(0);

  public constructor(context: MockAudioContext) {
    super(context, 'panner');
  }
}

/** Analyser double: `getFloatTimeDomainData` copies {@link MockAnalyserNode.samples}. */
export class MockAnalyserNode extends MockAudioNode {
  public fftSize = 2048;
  public samples: number[] = [];

  public constructor(context: MockAudioContext) {
    super(context, 'analyser');
  }

  public getFloatTimeDomainData(target: Float32Array): void {
    target.fill(0);
    target.set(this.samples.slice(0, target.length));
  }
}

/** AudioContext double; `currentTime` is set by tests. */
export class MockAudioContext {
  public currentTime = 0;
  public readonly sampleRate: number;
  public state: 'suspended' | 'running' | 'closed' = 'suspended';
  public readonly destination: MockAudioNode;
  public readonly sources: MockAudioBufferSourceNode[] = [];
  public readonly gains: MockGainNode[] = [];
  public readonly panners: MockStereoPannerNode[] = [];
  public readonly analysers: MockAnalyserNode[] = [];
  public readonly decoded: ArrayBuffer[] = [];
  public resumeCount = 0;
  /** Duration (s) of buffers returned by `decodeAudioData`, keyed by byte length; default 10 s. */
  public readonly decodeDurations = new Map<number, number>();
  /** When set, `decodeAudioData` rejects with this error. */
  public decodeError: Error | null = null;

  public constructor(sampleRate = 44100) {
    this.sampleRate = sampleRate;
    this.destination = new MockAudioNode(this, 'destination');
  }

  public createBufferSource(): MockAudioBufferSourceNode {
    return this.track(this.sources, new MockAudioBufferSourceNode(this));
  }

  public createGain(): MockGainNode {
    return this.track(this.gains, new MockGainNode(this));
  }

  public createStereoPanner(): MockStereoPannerNode {
    return this.track(this.panners, new MockStereoPannerNode(this));
  }

  public createAnalyser(): MockAnalyserNode {
    return this.track(this.analysers, new MockAnalyserNode(this));
  }

  public createBuffer(channels: number, length: number, sampleRate: number): MockAudioBuffer {
    return new MockAudioBuffer(channels, length, sampleRate);
  }

  public decodeAudioData(data: ArrayBuffer): Promise<MockAudioBuffer> {
    this.decoded.push(data);
    if (this.decodeError !== null) {
      return Promise.reject(this.decodeError);
    }
    const seconds = this.decodeDurations.get(data.byteLength) ?? 10;
    return Promise.resolve(new MockAudioBuffer(2, Math.round(seconds * this.sampleRate), this.sampleRate));
  }

  public resume(): Promise<void> {
    this.resumeCount += 1;
    this.state = 'running';
    return Promise.resolve();
  }

  public close(): Promise<void> {
    this.state = 'closed';
    return Promise.resolve();
  }

  /** Sources that were started, in creation order. */
  public startedSources(): MockAudioBufferSourceNode[] {
    return this.sources.filter((s) => s.startCalls.length > 0);
  }

  /** Casts the double to the DOM type expected by the engine. */
  public asAudioContext(): AudioContext {
    return this as unknown as AudioContext;
  }

  private track<T>(list: T[], node: T): T {
    list.push(node);
    return node;
  }
}
