/**
 * Per-run copies of a WAV fixture with a new content hash (feature 002).
 *
 * The worker caches standard stems and detail results per audio SHA-256 (FR-010), so a spec that
 * must observe a *live* detail phase on a rerun against the same data volume needs audio the
 * stack has not seen. The copy changes the first 32 samples of the PCM data by one
 * least-significant bit (inaudible; the fake models give the same outcomes).
 *
 * @packageDocumentation
 */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const outDir = path.resolve(__dirname, '..', '.state', 'audio');
const touchedSamples = 32;

/**
 * Writes `<fixture stem>-<salt>.wav` next to the suite's hand-over state and returns its path.
 * @param source A 16-bit PCM WAV file.
 * @param salt Text that makes the copy unique (run id, browser project, spec).
 */
export function uniqueWavCopy(source: string, salt: string): string {
  const bytes = Buffer.from(readFileSync(source));
  const data = pcmDataOffset(bytes);
  const bits = createHash('sha256').update(salt).digest();
  for (let i = 0; i < touchedSamples; i++) {
    const at = data + 2 * i;
    const sample = bytes.readInt16LE(at);
    const step = ((bits[i] ?? 0) & 1) === 1 ? 1 : -1;
    bytes.writeInt16LE(Math.max(-32_768, Math.min(32_767, sample + step)), at);
  }
  mkdirSync(outDir, { recursive: true });
  const safeSalt = salt.replace(/[^a-z0-9-]/gi, '-');
  const target = path.join(outDir, `${path.basename(source, '.wav')}-${safeSalt}.wav`);
  writeFileSync(target, bytes);
  return target;
}

/** Byte offset of the samples of the `data` chunk of a RIFF/WAVE file. */
function pcmDataOffset(bytes: Buffer): number {
  if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WAVE') {
    throw new Error('not a RIFF/WAVE file');
  }
  let offset = 12;
  while (offset + 8 <= bytes.length) {
    const size = bytes.readUInt32LE(offset + 4);
    if (bytes.toString('ascii', offset, offset + 4) === 'data') {
      return offset + 8;
    }
    offset += 8 + size + (size % 2);
  }
  throw new Error('WAVE file without a data chunk');
}
