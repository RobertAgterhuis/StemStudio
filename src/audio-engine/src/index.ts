/**
 * StemStudio audio engine entry point (ES module loaded by Blazor via JS interop).
 *
 * Placeholder: the audio-engine agent implements {@link createEngine} against `types.ts`.
 *
 * @packageDocumentation
 */
import type { AudioEngine, DotNetObjectReference, EngineContractVersion, EngineOptions } from './types';

export type * from './types';

/** Version of the engine contract implemented by this module. */
export const ENGINE_CONTRACT_VERSION: EngineContractVersion = '1.0.0';

/**
 * Creates the engine with its single AudioContext.
 *
 * @param dotnetRef - .NET object receiving `OnPositionChanged` / `OnPlaybackStateChanged`.
 * @param options - Optional overrides (tests inject a mocked AudioContext here).
 * @returns The engine instance.
 */
export function createEngine(dotnetRef: DotNetObjectReference, options?: EngineOptions): AudioEngine {
  throw new Error('audio engine not implemented yet', { cause: { dotnetRef, options } });
}
