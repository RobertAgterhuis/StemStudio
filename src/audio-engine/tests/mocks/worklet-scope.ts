import type { ProcessorConstructor, WorkletScope } from '../../src/stretch/processor';

/** Message port double of the processor side: records `postMessage`, `send` delivers to `onmessage`. */
export class FakeProcessorPort {
  public readonly posted: unknown[] = [];
  public onmessage: ((event: MessageEvent) => void) | null = null;

  public postMessage(message: unknown): void {
    this.posted.push(message);
  }

  public send(data: unknown): void {
    this.onmessage?.({ data } as MessageEvent);
  }

  /** Posted messages of one type. */
  public ofType(type: string): Record<string, unknown>[] {
    return (this.posted as Record<string, unknown>[]).filter((m) => m['type'] === type);
  }
}

/** Base class standing in for `AudioWorkletProcessor`. */
export class FakeProcessorBase {
  public readonly port = new FakeProcessorPort();
}

/** Fake `AudioWorkletGlobalScope`: `registerProcessor`, `currentFrame`, `sampleRate`. */
export class FakeWorkletScope implements WorkletScope {
  public readonly AudioWorkletProcessor = FakeProcessorBase as unknown as WorkletScope['AudioWorkletProcessor'];
  public readonly registered = new Map<string, ProcessorConstructor>();
  public currentFrame = 0;
  public sampleRate = 44100;

  public registerProcessor(name: string, processor: ProcessorConstructor): void {
    this.registered.set(name, processor);
  }
}
