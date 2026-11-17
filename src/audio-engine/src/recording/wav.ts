/**
 * 24-bit PCM WAV encoder of a take (research R-07, FR-011): canonical 44-byte header, `WAVE_FORMAT_PCM`,
 * little-endian, 1-2 channels. Samples use the libsndfile scale of ADR-016, `clamp(round(x * 2^23), -2^23, 2^23 - 1)`,
 * so the worker's render reads exactly these integers.
 *
 * @packageDocumentation
 */

/** Size of the canonical header (bytes). */
export const WAV_HEADER_BYTES = 44;
/** Bytes per 24-bit sample. */
const SAMPLE_BYTES = 3;
const SCALE = 0x800000;
const MAX_INT24 = SCALE - 1;

/**
 * @param x - Float sample.
 * @returns The 24-bit integer: `round(x * 2^23)` (half up) clamped to -2^23..2^23-1; NaN -> 0.
 */
export function quantise24(x: number): number {
  if (Number.isNaN(x)) {
    return 0;
  }
  return Math.min(MAX_INT24, Math.max(-SCALE, Math.round(x * SCALE))) + 0;
}

function writeAscii(view: DataView, at: number, text: string): void {
  for (let i = 0; i < text.length; i++) {
    view.setUint8(at + i, text.charCodeAt(i));
  }
}

function writeHeader(view: DataView, format: { channels: number; sampleRate: number; dataBytes: number }): void {
  const blockAlign = format.channels * SAMPLE_BYTES;
  writeAscii(view, 0, 'RIFF');
  view.setUint32(4, 36 + format.dataBytes, true);
  writeAscii(view, 8, 'WAVE');
  writeAscii(view, 12, 'fmt ');
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, format.channels, true);
  view.setUint32(24, format.sampleRate, true);
  view.setUint32(28, format.sampleRate * blockAlign, true);
  view.setUint16(32, blockAlign, true);
  view.setUint16(34, 24, true);
  writeAscii(view, 36, 'data');
  view.setUint32(40, format.dataBytes, true);
}

function validate(channels: readonly Float32Array[]): number {
  if (![1, 2].includes(channels.length)) {
    throw new Error(`WAV needs 1 or 2 channels (got ${String(channels.length)})`);
  }
  const frames = Math.max(...channels.map((channel) => channel.length));
  if (channels.some((channel) => channel.length !== frames)) {
    throw new Error('WAV channels differ in length');
  }
  return frames;
}

/**
 * @param channels - One (mono) or two (stereo) channels of equal length.
 * @param sampleRate - Sample rate written to the header; default 44100.
 * @returns The complete WAV file.
 * @throws Error for 0 or more than 2 channels, or channels of different length.
 */
export function encodeWav24(channels: readonly Float32Array[], sampleRate = 44100): ArrayBuffer {
  const frames = validate(channels);
  const dataBytes = frames * channels.length * SAMPLE_BYTES;
  const buffer = new ArrayBuffer(WAV_HEADER_BYTES + dataBytes);
  const view = new DataView(buffer);
  writeHeader(view, { channels: channels.length, sampleRate, dataBytes });
  const bytes = new Uint8Array(buffer);
  let at = WAV_HEADER_BYTES;
  for (let frame = 0; frame < frames; frame++) {
    for (const channel of channels) {
      const value = quantise24(channel[frame] ?? 0) & 0xffffff;
      bytes[at] = value & 0xff;
      bytes[at + 1] = (value >> 8) & 0xff;
      bytes[at + 2] = value >> 16;
      at += SAMPLE_BYTES;
    }
  }
  return buffer;
}
