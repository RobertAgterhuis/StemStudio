/**
 * Once-per-context loading of an AudioWorklet module, shared by the capture and the pitch worklets.
 *
 * @packageDocumentation
 */

/** Creates an `AudioWorkletNode` (injected so tests need no real worklet). */
export type WorkletNodeCreator = (context: AudioContext, name: string, options: AudioWorkletNodeOptions) => AudioWorkletNode;

/** A module loader that adds a module at most once per context. */
export type ModuleLoader = (context: AudioContext, url: string) => Promise<void>;

/**
 * @param failure - Builds the error message of a failed load from the cause.
 * @returns A loader; a failed load is forgotten so the next call retries.
 */
export function onceLoader(failure: (cause: string) => string): ModuleLoader {
  const modules = new WeakMap<BaseAudioContext, Promise<void>>();
  return (context, url) => {
    let loading = modules.get(context);
    if (loading === undefined) {
      loading = context.audioWorklet.addModule(url).catch((error: unknown) => {
        modules.delete(context);
        throw new Error(failure(String(error)), { cause: error });
      });
      modules.set(context, loading);
    }
    return loading;
  };
}
