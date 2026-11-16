import type { SoundTouchExports } from '../../src/stretch/abi';

/** One recorded `st_set_pitch` call with the number of frames put since the last clear at that moment. */
export interface PitchCall {
  readonly value: number;
  readonly inFrames: number;
}

/**
 * TypeScript stand-in for `soundtouch.wasm` (WASM ABI v1): an identity "SoundTouch" that holds back
 * `latency` frames (optionally varied per receive by `jitter`) and records every call.
 */
export class FakeSoundTouch implements SoundTouchExports {
  public readonly memory = new WebAssembly.Memory({ initial: 64, maximum: 64 });
  public readonly settings: [number, number][] = [];
  public readonly pitches: PitchCall[] = [];
  public readonly created: [number, number][] = [];
  public readonly destroyed: number[] = [];
  public readonly freed: number[] = [];
  public mallocs = 0;
  public clears = 0;
  public initialized = 0;
  public latency = 0;
  public jitter: () => number = () => 0;
  public createResult = 4096;
  public abiVersion = 1;
  /** When set, `st_malloc` returns this instead of a pointer. */
  public mallocResult: number | null = null;
  private queue: [number, number][] = [];
  private putFrames = 0;
  private heap = 1024;

  public _initialize(): void {
    this.initialized += 1;
  }

  public st_abi_version(): number {
    return this.abiVersion;
  }

  public st_create(sampleRate: number, channels: number): number {
    this.created.push([sampleRate, channels]);
    return this.createResult;
  }

  public st_destroy(handle: number): void {
    this.destroyed.push(handle);
  }

  public st_set_pitch(_handle: number, pitch: number): void {
    this.pitches.push({ value: pitch, inFrames: this.putFrames });
  }

  public st_set_setting(_handle: number, id: number, value: number): number {
    this.settings.push([id, value]);
    return 1;
  }

  public st_initial_latency(): number {
    return this.latency;
  }

  public st_put(_handle: number, samples: number, frames: number): void {
    const view = new Float32Array(this.memory.buffer, samples, frames * 2);
    for (let i = 0; i < frames; i++) {
      this.queue.push([Number(view[2 * i]), Number(view[2 * i + 1])]);
    }
    this.putFrames += frames;
  }

  public st_receive(_handle: number, samples: number, maxFrames: number): number {
    const available = Math.max(0, this.queue.length - this.latency - this.jitter());
    const frames = Math.min(available, maxFrames);
    const view = new Float32Array(this.memory.buffer, samples, frames * 2);
    this.queue.splice(0, frames).forEach(([left, right], i) => {
      view[2 * i] = left;
      view[2 * i + 1] = right;
    });
    return frames;
  }

  public st_clear(): void {
    this.clears += 1;
    this.queue = [];
    this.putFrames = 0;
  }

  public st_malloc(bytes: number): number {
    this.mallocs += 1;
    if (this.mallocResult !== null) {
      return this.mallocResult;
    }
    const pointer = this.heap;
    this.heap += Math.ceil(bytes / 16) * 16;
    return pointer;
  }

  public st_free(pointer: number): void {
    this.freed.push(pointer);
  }

  /** Frames put since the last clear. */
  public get inFrames(): number {
    return this.putFrames;
  }
}
