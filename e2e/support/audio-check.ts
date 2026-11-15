/**
 * Engine-free checks of exported audio (feature 007): WAV headers are read in Node; FLAC, WAV
 * and MP3 bodies are decoded by the browser's own `decodeAudioData` in a blank scratch page (an
 * `OfflineAudioContext` at 44.1 kHz, so nothing is resampled). Both sides of every comparison go
 * through the same decoder, so "equal samples" means equal PCM.
 *
 * @packageDocumentation
 */
import type { Page } from '@playwright/test';
import type { PanMatrix } from './render-vectors';

/** Header facts of a PCM WAV file. */
export interface WavFacts {
  readonly sampleRate: number;
  readonly channels: number;
  readonly bitsPerSample: number;
  readonly frames: number;
  readonly durationSeconds: number;
}

/** One input of an expected mix: a decoded file, its gain and its pan matrix. */
export interface MixInput {
  readonly base64: string;
  readonly gain: number;
  readonly matrix: PanMatrix;
}

/** RMS per channel (linear, full scale = 1). */
export type ChannelRms = readonly [number, number];

/** Reads `fmt ` and `data` of a RIFF/WAVE file. */
export function wavFacts(bytes: Buffer): WavFacts {
  if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WAVE') {
    throw new Error('not a RIFF/WAVE file');
  }
  const fmt = chunk(bytes, 'fmt ');
  const channels = bytes.readUInt16LE(fmt + 2);
  const sampleRate = bytes.readUInt32LE(fmt + 4);
  const bitsPerSample = bytes.readUInt16LE(fmt + 14);
  const data = chunk(bytes, 'data');
  const frames = bytes.readUInt32LE(data - 4) / (channels * (bitsPerSample / 8));
  return { sampleRate, channels, bitsPerSample, frames, durationSeconds: frames / sampleRate };
}

/** Offset of the body of the first chunk with this id. */
function chunk(bytes: Buffer, id: string): number {
  for (let at = 12; at + 8 <= bytes.length; at += 8 + even(bytes.readUInt32LE(at + 4))) {
    if (bytes.toString('ascii', at, at + 4) === id) return at + 8;
  }
  throw new Error(`WAVE file without a "${id}" chunk`);
}

function even(size: number): number {
  return size + (size % 2);
}

/** True when both files decode to exactly the same samples (frame count and every value). */
export async function samplesEqual(scratch: Page, a: Buffer, b: Buffer): Promise<boolean> {
  return scratch.evaluate(
    async ([x, y]) => {
      const decode = async (text: string): Promise<AudioBuffer> =>
        new OfflineAudioContext(2, 1, 44_100).decodeAudioData(Uint8Array.from(atob(text), (c) => c.charCodeAt(0)).buffer);
      const [p, q] = await Promise.all([decode(x), decode(y)]);
      const same = (c: number): boolean => p.getChannelData(c).every((v, i) => v === q.getChannelData(c)[i]);
      return p.length === q.length && p.numberOfChannels === q.numberOfChannels && same(0) && same(1);
    },
    [a.toString('base64'), b.toString('base64')] as const,
  );
}

/**
 * RMS per channel of `master * sum(gain_i * matrix_i * input_i)`, the render law of research.md
 * E-05; with one input at gain 1 and the identity matrix it is the RMS of the file itself. The
 * matrix is applied with explicit splitter/gain/merger nodes (no StereoPannerNode), so the
 * expectation does not depend on the browser's panner.
 */
export async function mixRms(scratch: Page, inputs: readonly MixInput[], master: number): Promise<ChannelRms> {
  return scratch.evaluate(renderMixInPage, { list: inputs, gain: master });
}

/** Runs inside the scratch page (serialised by Playwright, so it must be self-contained). */
async function renderMixInPage({ list, gain }: { list: readonly MixInput[]; gain: number }): Promise<ChannelRms> {
  const bytes = (text: string): ArrayBuffer => Uint8Array.from(atob(text), (c) => c.charCodeAt(0)).buffer;
  const probe = new OfflineAudioContext(2, 1, 44_100);
  const buffers = await Promise.all(list.map((input) => probe.decodeAudioData(bytes(input.base64))));
  const context = new OfflineAudioContext(2, Math.min(...buffers.map((b) => b.length)), 44_100);
  const merger = new ChannelMergerNode(context, { numberOfInputs: 2 });
  merger.connect(context.destination);
  buffers.forEach((buffer, n) => {
    const source = new AudioBufferSourceNode(context, { buffer });
    const splitter = new ChannelSplitterNode(context, { numberOfOutputs: 2 });
    source.connect(splitter);
    list[n]?.matrix.forEach((row, out) => {
      row.forEach((k, from) => {
        const node = new GainNode(context, { gain: gain * (list[n]?.gain ?? 0) * k });
        splitter.connect(node, from).connect(merger, 0, out);
      });
    });
    source.start();
  });
  const rendered = await context.startRendering();
  const rms = (c: number): number => {
    const data = rendered.getChannelData(c);
    return Math.sqrt(data.reduce((sum, v) => sum + v * v, 0) / data.length);
  };
  return [rms(0), rms(1)] as const;
}

/** Decibel difference of two linear levels. */
export function dbDifference(actual: number, expected: number): number {
  return 20 * Math.log10(actual / expected);
}
