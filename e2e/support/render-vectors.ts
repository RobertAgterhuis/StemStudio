/**
 * The shared render vectors of feature 007 (`specs/007-export-library/contracts/render-vectors.json`
 * 1.0.0): the stereo panner law as 2x2 matrices (research.md E-05). The browser is the fourth
 * consumer next to the engine, backend and worker suites (panner-law.spec.ts).
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';

/** `[[a, b], [c, d]]` with `L' = a*L + b*R`, `R' = c*L + d*R`. */
export type PanMatrix = readonly [readonly [number, number], readonly [number, number]];

/** One pan case of the vectors. */
export interface PanVector {
  readonly pan: number;
  readonly matrix: PanMatrix;
}

const vectorFile = path.resolve(__dirname, '..', '..', 'specs', '007-export-library', 'contracts', 'render-vectors.json');

/** Reads the pan cases of the committed vectors. */
export function readPanVectors(): PanVector[] {
  const document = JSON.parse(readFileSync(vectorFile, 'utf-8')) as { version: string; pan: PanVector[] };
  return document.pan;
}

/**
 * The matrix of one pan value from the vectors.
 * @throws Error when the vectors have no case for `pan` (add one with generate_render_vectors.py).
 */
export function panMatrix(pan: number): PanMatrix {
  const found = readPanVectors().find((vector) => Math.abs(vector.pan - pan) < 1e-9);
  if (found === undefined) {
    throw new Error(`render-vectors.json has no pan case for ${String(pan)}`);
  }
  return found.matrix;
}

/** Applies a matrix to one stereo sample. */
export function applyPan(matrix: PanMatrix, left: number, right: number): [number, number] {
  const [[a, b], [c, d]] = matrix;
  return [a * left + b * right, c * left + d * right];
}
