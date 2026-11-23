/** Test signals for the pitch core (generated, deterministic). */

/** `seconds` of a sum of partials `[frequency, amplitude]` at `rate`. */
export function tone(rate: number, seconds: number, partials: readonly (readonly [number, number])[]): Float32Array {
  const out = new Float32Array(Math.round(rate * seconds));
  for (let i = 0; i < out.length; i += 1) {
    out[i] = partials.reduce((sum, [f, a]) => sum + a * Math.sin((2 * Math.PI * f * i) / rate), 0);
  }
  return out;
}

/** Deterministic white noise in -amplitude..amplitude. */
export function noise(rate: number, seconds: number, amplitude: number): Float32Array {
  const out = new Float32Array(Math.round(rate * seconds));
  let seed = 12_345;
  for (let i = 0; i < out.length; i += 1) {
    seed = (seed * 1_103_515_245 + 12_345) % 2_147_483_648;
    out[i] = amplitude * ((seed / 2_147_483_648) * 2 - 1);
  }
  return out;
}

/** Cents of a frequency (MIDI x 100). */
export function centsOf(hz: number): number {
  return 1200 * Math.log2(hz / 440) + 6900;
}

/** Median of a list. */
export function median(values: readonly number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)] ?? Number.NaN;
}
