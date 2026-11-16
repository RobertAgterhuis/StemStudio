/**
 * Measured constants of the time-stretch stage (Phase 0 P0-1, ADR-021 P5/P6, data-model 1). Shared by the engine
 * (main thread) and the AudioWorklet processor, so this module has no imports and no DOM use.
 *
 * @packageDocumentation
 */

/** Frames of one Web Audio render quantum. */
export const RENDER_QUANTUM_FRAMES = 128;

/**
 * Fixed FIFO delay D of the stage in frames: SoundTouch output frame `m` is played at stage output frame `m + D`.
 * max(initial latency 4,852, output lag 4,736) + 2 quanta, rounded up to 128 = 116.1 ms at 44.1 kHz.
 */
export const STRETCH_DELAY_FRAMES = 5120;

/** Frames the FIFO may grow above D before the oldest surplus is dropped (counted as underruns). */
export const STRETCH_OVERFLOW_FRAMES = 1024;

/** Slope of the mean WSOLA content offset `c(p) = round(slope * log2(p))` in frames per octave of pitch factor. */
export const STRETCH_CONTENT_FRAMES_PER_OCTAVE = -1100;

/** Version returned by `st_abi_version()` of a compatible `soundtouch.wasm`. */
export const STRETCH_ABI_VERSION = 1;

/** Lead time (s) between a live speed/transpose call and the context time `U` it enters the stage. */
export const CHANGE_LEAD_SECONDS = 0.02;

/** Name the processor is registered under in the AudioWorkletGlobalScope. */
export const STRETCH_PROCESSOR_NAME = 'stemstudio-time-stretch';

/**
 * SoundTouch settings sent once after `st_create` (research.md R-04), as `[settingId, value]`:
 * quick seek off, anti-alias filter on, 64 filter taps, 8 ms overlap.
 */
export const SOUNDTOUCH_SETTINGS: readonly (readonly [number, number])[] = [
  [2, 0],
  [0, 1],
  [1, 64],
  [5, 8],
];
