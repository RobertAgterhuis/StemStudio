import { MockAudioContext, MockAudioNode } from './audio-context';

/** Main-thread side of a worklet node's port: records `postMessage`, `reply` delivers processor messages. */
export class MockNodePort {
  public readonly posted: unknown[] = [];
  public onmessage: ((event: MessageEvent) => void) | null = null;

  public postMessage(message: unknown): void {
    this.posted.push(message);
  }

  public reply(data: unknown): void {
    this.onmessage?.({ data } as MessageEvent);
  }
}

/** `AudioWorkletNode` double. */
export class MockAudioWorkletNode extends MockAudioNode {
  public readonly port = new MockNodePort();
  public onprocessorerror: ((event: Event) => void) | null = null;

  public constructor(
    context: MockAudioContext,
    public readonly name: string,
    public readonly options: AudioWorkletNodeOptions,
  ) {
    super(context, 'worklet');
  }
}

/** `AudioWorklet` double: records `addModule` URLs; `error` makes it reject. */
export class MockAudioWorklet {
  public readonly modules: string[] = [];
  public error: Error | null = null;

  public addModule(url: string): Promise<void> {
    this.modules.push(url);
    return this.error === null ? Promise.resolve() : Promise.reject(this.error);
  }
}

/** A mock context with an `audioWorklet` and a node factory that records created worklet nodes. */
export class WorkletContext extends MockAudioContext {
  public readonly audioWorklet = new MockAudioWorklet();
  public readonly workletNodes: MockAudioWorkletNode[] = [];

  public createWorkletNode(name: string, options: AudioWorkletNodeOptions): MockAudioWorkletNode {
    const node = new MockAudioWorkletNode(this, name, options);
    this.workletNodes.push(node);
    return node;
  }
}
