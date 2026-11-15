/**
 * StemStudio audio engine entry point (ES module loaded by Blazor via JS interop).
 *
 * @packageDocumentation
 */
import { Engine } from './engine';
import type { AudioEngine, DotNetObjectReference, EngineContractVersion, EngineOptions } from './types';

export type * from './types';

/** Version of the engine contract implemented by this module. */
export const ENGINE_CONTRACT_VERSION: EngineContractVersion = '2.1.0';

/**
 * Creates the engine with its single AudioContext.
 *
 * @param dotnetRef - .NET object receiving `OnPositionChanged` / `OnPlaybackStateChanged`.
 * @param options - Optional overrides (tests inject a mocked AudioContext here).
 * @returns The engine instance.
 */
export function createEngine(dotnetRef: DotNetObjectReference, options?: EngineOptions): AudioEngine {
  return new Engine(dotnetRef, options);
}
