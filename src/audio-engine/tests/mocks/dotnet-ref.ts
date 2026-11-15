import type { DotNetCallbacks, DotNetObjectReference } from '../../src/types';

/** One recorded .NET callback. */
export interface DotNetCall {
  readonly method: DotNetCallbacks;
  readonly args: readonly unknown[];
}

/** Fake `DotNetObjectReference` that records every `invokeMethodAsync` call. */
export class FakeDotNetRef implements DotNetObjectReference {
  public readonly calls: DotNetCall[] = [];

  public invokeMethodAsync(methodName: DotNetCallbacks, ...args: unknown[]): Promise<unknown> {
    this.calls.push({ method: methodName, args });
    return Promise.resolve(undefined);
  }

  /** Arguments of every call to `method`, first argument only. */
  public argsOf(method: DotNetCallbacks): unknown[] {
    return this.calls.filter((c) => c.method === method).map((c) => c.args[0]);
  }

  /** Forgets all recorded calls. */
  public clear(): void {
    this.calls.length = 0;
  }
}
