/** Signal helpers of the WASM suite: test signals, an FFT peak estimate and burst onsets. */

/** Mono sine of `frames` samples. */
export function sine(frequency: number, frames: number, sampleRate: number, amplitude = 0.5): Float32Array {
  return Float32Array.from({ length: frames }, (_, i) => amplitude * Math.sin((2 * Math.PI * frequency * i) / sampleRate));
}

/** Deterministic white noise -1..1 (LCG). */
export function noise(frames: number, seed = 1): Float32Array {
  let state = seed;
  return Float32Array.from({ length: frames }, () => {
    state = (state * 1664525 + 1013904223) % 2 ** 32;
    return (state / 2 ** 32) * 2 - 1;
  });
}

/** Noise bursts of `burstFrames` every `periodFrames`, silence in between; returns the signal and the onsets. */
export function bursts(frames: number, periodFrames: number, burstFrames: number): { signal: Float32Array; onsets: number[] } {
  const signal = new Float32Array(frames);
  const source = noise(frames, 3);
  const onsets: number[] = [];
  for (let start = periodFrames; start + burstFrames < frames; start += periodFrames) {
    onsets.push(start);
    for (let i = 0; i < burstFrames; i++) {
      signal[start + i] = 0.5 * Number(source[start + i]);
    }
  }
  return { signal, onsets };
}

/** Bit-reversal permutation of the FFT input. */
function bitReverse(re: Float64Array): void {
  const n = re.length;
  for (let i = 1, j = 0; i < n; i++) {
    let bit = n >> 1;
    for (; j & bit; bit >>= 1) {
      j ^= bit;
    }
    j ^= bit;
    if (i < j) {
      [re[i], re[j]] = [Number(re[j]), Number(re[i])];
    }
  }
}

/** One radix-2 butterfly of the pair (a, b) with twiddle angle `angle`. */
function butterfly(re: Float64Array, im: Float64Array, [a, b]: [number, number], angle: number): void {
  const [wr, wi] = [Math.cos(angle), Math.sin(angle)];
  const tr = wr * Number(re[b]) - wi * Number(im[b]);
  const ti = wr * Number(im[b]) + wi * Number(re[b]);
  re[b] = Number(re[a]) - tr;
  im[b] = Number(im[a]) - ti;
  re[a] = Number(re[a]) + tr;
  im[a] = Number(im[a]) + ti;
}

/** In-place iterative radix-2 FFT of a real input (`im` all zero). */
function fft(re: Float64Array, im: Float64Array): void {
  bitReverse(re);
  for (let size = 2; size <= re.length; size <<= 1) {
    for (let start = 0; start < re.length; start += size) {
      for (let k = 0; k < size / 2; k++) {
        butterfly(re, im, [start + k, start + k + size / 2], (-2 * Math.PI * k) / size);
      }
    }
  }
}

/** Frequency (Hz) of the strongest FFT bin of `signal` (Hann window, parabolic interpolation). */
export function peakFrequency(signal: Float32Array, sampleRate: number, size = 65536): number {
  const re = new Float64Array(size);
  const im = new Float64Array(size);
  for (let i = 0; i < size; i++) {
    re[i] = Number(signal[i]) * (0.5 - 0.5 * Math.cos((2 * Math.PI * i) / (size - 1)));
  }
  fft(re, im);
  const magnitude = (k: number): number => Math.hypot(Number(re[k]), Number(im[k]));
  let best = 1;
  for (let k = 2; k < size / 2; k++) {
    best = magnitude(k) > magnitude(best) ? k : best;
  }
  const [a, b, c] = [magnitude(best - 1), magnitude(best), magnitude(best + 1)];
  const shift = (0.5 * (a - c)) / (a - 2 * b + c);
  return ((best + shift) * sampleRate) / size;
}

/** 64-frame moving mean of the absolute value. */
function envelope(signal: Float32Array): Float32Array {
  const result = new Float32Array(signal.length);
  let sum = 0;
  signal.forEach((value, i) => {
    sum += Math.abs(value) - Math.abs(signal[i - 64] ?? 0);
    result[i] = sum / 64;
  });
  return result;
}

/** First frame after each silence of at least `minGap` frames where the envelope exceeds `threshold`. */
export function envelopeOnsets(signal: Float32Array, threshold = 0.05, minGap = 2048): number[] {
  const onsets: number[] = [];
  let last = -Infinity;
  envelope(signal).forEach((level, i) => {
    if (level > threshold) {
      onsets.push(...(i - last > minGap ? [i] : []));
      last = i;
    }
  });
  return onsets;
}
