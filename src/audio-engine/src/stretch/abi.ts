/**
 * WASM ABI v1 of `soundtouch.wasm` (audio-engine.md 2.3.0 "WASM ABI", `third_party/soundtouch/include/
 * stemstudio_soundtouch.h`) and a stereo stream over one SoundTouch instance with buffers allocated once.
 * Used by the AudioWorklet processor and by the Node WASM suite; no DOM use.
 *
 * @packageDocumentation
 */
import { RENDER_QUANTUM_FRAMES, SOUNDTOUCH_SETTINGS, STRETCH_ABI_VERSION } from './constants';
import type { DelayFifo } from './fifo';

/** Exports of a module implementing WASM ABI v1 (pointers and handles are offsets into `memory`). */
export interface SoundTouchExports {
  /** Exported linear memory (fixed size). */
  readonly memory: WebAssembly.Memory;
  /** Reactor initialisation (static constructors); called once before any other export when present. */
  readonly _initialize?: () => void;
  /** Returns the ABI version (1). */
  st_abi_version(): number;
  /** Creates an instance; 0 on failure. */
  st_create(sampleRate: number, channels: number): number;
  /** Destroys an instance. */
  st_destroy(handle: number): void;
  /** `SoundTouch::setPitch`. */
  st_set_pitch(handle: number, pitch: number): void;
  /** `SoundTouch::setSetting`; 1 when accepted. */
  st_set_setting(handle: number, id: number, value: number): number;
  /** Initial latency in frames. */
  st_initial_latency(handle: number): number;
  /** Puts interleaved frames. */
  st_put(handle: number, samples: number, frames: number): void;
  /** Receives at most `maxFrames` interleaved frames; returns the number written. */
  st_receive(handle: number, samples: number, maxFrames: number): number;
  /** Drops all buffered samples. */
  st_clear(handle: number): void;
  /** Allocates bytes in linear memory; 0 on failure. */
  st_malloc(bytes: number): number;
  /** Frees a pointer of `st_malloc`. */
  st_free(pointer: number): void;
}

/** Turns module bytes into ABI exports (the real loader or a test fake). */
export type AbiLoader = (bytes: BufferSource) => Promise<SoundTouchExports>;

/** Frames received per `st_receive` call. */
export const RECEIVE_CHUNK_FRAMES = 2048;

const BYTES_PER_FRAME = 8;

/**
 * Instantiates `soundtouch.wasm` (no imports, Phase 0 P8) and runs its reactor initialisation.
 *
 * @param bytes - Module bytes (fetched by the main thread, passed in `processorOptions.wasmBytes`).
 * @returns The ABI exports.
 */
export async function instantiateSoundTouch(bytes: BufferSource): Promise<SoundTouchExports> {
  const source = await WebAssembly.instantiate(bytes, {});
  const exports = source.instance.exports as unknown as SoundTouchExports;
  exports._initialize?.();
  return exports;
}

/** One stereo SoundTouch instance with an input block buffer and an output chunk buffer in linear memory. */
export class SoundTouchStream {
  private readonly input: Float32Array;
  private readonly output: Float32Array;

  private constructor(
    private readonly abi: SoundTouchExports,
    private readonly handle: number,
    private readonly pointers: readonly [number, number],
  ) {
    const [inPtr, outPtr] = pointers;
    this.input = new Float32Array(abi.memory.buffer, inPtr, RENDER_QUANTUM_FRAMES * 2);
    this.output = new Float32Array(abi.memory.buffer, outPtr, RECEIVE_CHUNK_FRAMES * 2);
  }

  /**
   * Checks the ABI version, creates the instance, applies the R-04 settings and allocates the buffers.
   *
   * @param abi - Module exports.
   * @param sampleRate - Context sample rate.
   * @returns The stream.
   * @throws Error for a wrong ABI version or a failing `st_create`/`st_malloc`.
   */
  public static create(abi: SoundTouchExports, sampleRate: number): SoundTouchStream {
    const version = abi.st_abi_version();
    if (version !== STRETCH_ABI_VERSION) {
      throw new Error(`soundtouch.wasm has ABI version ${String(version)}, expected ${String(STRETCH_ABI_VERSION)}`);
    }
    const handle = abi.st_create(sampleRate, 2);
    if (handle === 0) {
      throw new Error('st_create failed');
    }
    SOUNDTOUCH_SETTINGS.forEach(([id, value]) => abi.st_set_setting(handle, id, value));
    const pointers = [allocate(abi, RENDER_QUANTUM_FRAMES), allocate(abi, RECEIVE_CHUNK_FRAMES)] as const;
    return new SoundTouchStream(abi, handle, pointers);
  }

  /** Interleaved view of the input block buffer (fill it, then {@link SoundTouchStream.put}). */
  public get block(): Float32Array {
    return this.input;
  }

  /**
   * Puts frames `from..to` of the input block buffer.
   *
   * @param from - First frame (inclusive).
   * @param to - Last frame (exclusive).
   */
  public put(from: number, to: number): void {
    if (to > from) {
      this.abi.st_put(this.handle, this.pointers[0] + from * BYTES_PER_FRAME, to - from);
    }
  }

  /**
   * Receives everything SoundTouch has ready into the FIFO.
   *
   * @param fifo - Fixed-delay FIFO.
   * @returns Frames received.
   */
  public drainInto(fifo: DelayFifo): number {
    let total = 0;
    for (;;) {
      const frames = this.abi.st_receive(this.handle, this.pointers[1], RECEIVE_CHUNK_FRAMES);
      fifo.write(this.output, frames);
      total += frames;
      if (frames < RECEIVE_CHUNK_FRAMES) {
        return total;
      }
    }
  }

  /** @param factor - Pitch factor. */
  public setPitch(factor: number): void {
    this.abi.st_set_pitch(this.handle, factor);
  }

  /** Drops all samples buffered inside SoundTouch. */
  public clear(): void {
    this.abi.st_clear(this.handle);
  }

  /** Destroys the instance and frees the buffers. */
  public destroy(): void {
    this.abi.st_destroy(this.handle);
    this.pointers.forEach((pointer) => {
      this.abi.st_free(pointer);
    });
  }
}

function allocate(abi: SoundTouchExports, frames: number): number {
  const pointer = abi.st_malloc(frames * BYTES_PER_FRAME);
  if (pointer === 0) {
    throw new Error('st_malloc failed');
  }
  return pointer;
}
