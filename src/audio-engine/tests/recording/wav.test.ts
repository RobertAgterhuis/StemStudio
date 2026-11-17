import { describe, expect, it } from 'vitest';
import { WAV_HEADER_BYTES, encodeWav24, quantise24 } from '../../src/recording/wav';

/** 24-bit PCM WAV encoder of takes (research R-07, FR-011). */
function ascii(view: DataView, at: number, length: number): string {
  return String.fromCharCode(...new Uint8Array(view.buffer, at, length));
}

function int24(view: DataView, at: number): number {
  const value = view.getUint8(at) | (view.getUint8(at + 1) << 8) | (view.getUint8(at + 2) << 16);
  return value >= 0x800000 ? value - 0x1000000 : value;
}

describe('quantise24', () => {
  it.each([
    [0, 0],
    [0.5, 4194304],
    [-0.5, -4194304],
    [1, 8388607],
    [1.5, 8388607],
    [-1, -8388608],
    [-2, -8388608],
    [Number.NaN, 0],
    [1 / 8388608, 1],
    [0.4 / 8388608, 0],
    [0.5 / 8388608, 1],
    [-0.5 / 8388608, 0],
  ])('%f -> %i', (x, expected) => {
    expect(quantise24(x)).toBe(expected);
  });
});

describe('encodeWav24', () => {
  it('writes the canonical 44-byte PCM header (mono)', () => {
    const view = new DataView(encodeWav24([new Float32Array(3)]));
    expect(WAV_HEADER_BYTES).toBe(44);
    expect(view.byteLength).toBe(44 + 9);
    expect([ascii(view, 0, 4), view.getUint32(4, true), ascii(view, 8, 4), ascii(view, 12, 4)]).toEqual(['RIFF', 36 + 9, 'WAVE', 'fmt ']);
    expect([view.getUint32(16, true), view.getUint16(20, true), view.getUint16(22, true)]).toEqual([16, 1, 1]);
    expect([view.getUint32(24, true), view.getUint32(28, true), view.getUint16(32, true), view.getUint16(34, true)]).toEqual([44100, 44100 * 3, 3, 24]);
    expect([ascii(view, 36, 4), view.getUint32(40, true)]).toEqual(['data', 9]);
  });

  it('writes stereo header fields and interleaves L/R frames', () => {
    const left = new Float32Array([0.5, -1]);
    const right = new Float32Array([-0.5, 1]);
    const view = new DataView(encodeWav24([left, right], 48000));
    expect([view.getUint16(22, true), view.getUint32(24, true), view.getUint32(28, true), view.getUint16(32, true)]).toEqual([2, 48000, 48000 * 6, 6]);
    expect(view.getUint32(40, true)).toBe(12);
    expect([0, 3, 6, 9].map((i) => int24(view, 44 + i))).toEqual([4194304, -4194304, -8388608, 8388607]);
  });

  it('is little-endian (byte vector)', () => {
    const bytes = new Uint8Array(encodeWav24([new Float32Array([1 / 8388608 * 0x123456, -1 / 8388608])]));
    expect([...bytes.slice(44)]).toEqual([0x56, 0x34, 0x12, 0xff, 0xff, 0xff]);
    expect([...bytes.slice(0, 4)]).toEqual([0x52, 0x49, 0x46, 0x46]);
  });

  it('writes an empty data chunk for zero frames', () => {
    const view = new DataView(encodeWav24([new Float32Array(0), new Float32Array(0)]));
    expect([view.byteLength, view.getUint32(40, true), view.getUint32(4, true)]).toEqual([44, 0, 36]);
  });

  it('rejects other channel counts and unequal lengths', () => {
    expect(() => encodeWav24([])).toThrow('WAV needs 1 or 2 channels (got 0)');
    expect(() => encodeWav24([new Float32Array(1), new Float32Array(1), new Float32Array(1)])).toThrow('WAV needs 1 or 2 channels (got 3)');
    expect(() => encodeWav24([new Float32Array(1), new Float32Array(2)])).toThrow('WAV channels differ in length');
  });
});
