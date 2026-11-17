/**
 * Capture mapping (data-model 3.2, research R-03): a captured context frame `f` belongs to song sample
 * `songSample(f) = heardSampleAt(f - C)`, evaluated on the transport segment heard at `f - C` (a play start, a loop
 * pass, the end of a count-in gap). `C` is the measured latency `L`; the tempo-stage latency `P` is already part of
 * the heard anchors (Phase 0 P0-8) and is not added again. Recording runs at rate 1, so every segment maps frames to
 * song samples one to one.
 *
 * @packageDocumentation
 */
import type { CaptureBuffer } from './capture-buffer';

/** Shortest partial loop pass that is kept as a take (1 s). */
export const MIN_PARTIAL_PASS_SAMPLES = 44100;

/** A heard transport segment in frames (from the session's segment log). */
export interface TransportSegment {
  /** Heard context frame of `offsetSample`. */
  readonly whenFrame: number;
  /** Song sample heard at `whenFrame`. */
  readonly offsetSample: number;
  /** Heard context frame where the segment's sources end (explicit pass), or null (open, native loop). */
  readonly untilFrame: number | null;
}

/** Enabled loop region in song samples. */
export interface LoopSpan {
  readonly startSample: number;
  readonly endSample: number;
}

/** Everything the mapping reads. */
export interface CaptureMapInput {
  /** Captured input. */
  readonly capture: CaptureBuffer;
  /** Segments of the session in `whenFrame` order. */
  readonly segments: readonly TransportSegment[];
  /** Loop region while recording, or null. */
  readonly loop: LoopSpan | null;
  /** Compensation `C` in frames (the latency `L`). */
  readonly compensation: number;
  /** Heard context frame at which playback stopped. */
  readonly stopFrame: number;
  /** Song length in samples. */
  readonly trackSamples: number;
  /** The session ended because the input was lost (a take without loop is then partial). */
  readonly interrupted?: boolean;
}

/** One take cut from the capture. */
export interface MappedTake {
  /** 1-based pass of the session; null without loop. */
  readonly passIndex: number | null;
  /** Not a complete loop pass (or an interrupted take without loop). */
  readonly partial: boolean;
  /** Song sample of the first take sample. */
  readonly startSample: number;
  readonly lengthSamples: number;
  /** Context frame the first take sample was captured at. */
  readonly firstFrame: number;
  /** Audio per channel. */
  readonly channels: Float32Array[];
  /** Capture gaps inside the take. */
  readonly dropouts: number;
}

/** A run of heard frames `[from, to)` playing song samples from `song`, in pass `pass`. */
interface Piece {
  readonly from: number;
  readonly to: number;
  readonly song: number;
  readonly pass: number | null;
}

/**
 * End of segment `index` in heard frames: the next segment or the stop. An explicit pass (`untilFrame`) without loop
 * also ends there; with the loop it is one pass that ends at the loop end (its rounded `untilFrame` may differ by a
 * frame, so the pass length comes from the loop, not from it).
 */
function segmentEnd(input: CaptureMapInput, segment: TransportSegment, index: number): number {
  const next = input.segments[index + 1]?.whenFrame ?? Infinity;
  const until = input.loop === null ? (segment.untilFrame ?? Infinity) : Infinity;
  return Math.min(until, next, input.stopFrame);
}

/** Pieces of one segment without loop: up to the end of the track. */
function openPieces(segment: TransportSegment, end: number, trackSamples: number): Piece[] {
  const to = Math.min(end, segment.whenFrame + trackSamples - segment.offsetSample);
  return [{ from: segment.whenFrame, to, song: segment.offsetSample, pass: null }];
}

/**
 * Pieces of one segment with loop: the (partial) first pass, then, for an open segment (native loop), whole passes
 * from the loop start; an explicit pass segment is exactly one pass.
 */
function loopPieces(segment: TransportSegment, end: number, loop: LoopSpan, firstPass: number): Piece[] {
  const pieces: Piece[] = [];
  const passes = segment.untilFrame === null ? Infinity : 1;
  let from = segment.whenFrame;
  let song = Math.min(segment.offsetSample, loop.endSample - 1);
  while (from < end && pieces.length < passes) {
    const to = from + (loop.endSample - song);
    pieces.push({ from, to: Math.min(to, end), song, pass: firstPass + pieces.length });
    from = to;
    song = loop.startSample;
  }
  return pieces;
}

/** Every piece of the session, numbered by transport pass. */
function piecesOf(input: CaptureMapInput): Piece[] {
  const pieces: Piece[] = [];
  let nextPass = 1;
  input.segments.forEach((segment, index) => {
    const end = segmentEnd(input, segment, index);
    const own = input.loop === null ? openPieces(segment, end, input.trackSamples) : loopPieces(segment, end, input.loop, nextPass);
    nextPass += own.length;
    pieces.push(...own);
  });
  return pieces;
}

/** Clips a piece to the frames actually captured (`f = g + C`). */
function clip(piece: Piece, input: CaptureMapInput): Piece {
  const from = Math.max(piece.from, input.capture.firstFrame - input.compensation);
  const to = Math.min(piece.to, input.capture.endFrame - input.compensation);
  return { ...piece, from, to, song: piece.song + (from - piece.from) };
}

function isPartial(piece: Piece, input: CaptureMapInput): boolean {
  if (input.loop === null) {
    return input.interrupted === true;
  }
  return piece.song !== input.loop.startSample || piece.song + (piece.to - piece.from) !== input.loop.endSample;
}

function toTake(piece: Piece, input: CaptureMapInput): MappedTake {
  const firstFrame = piece.from + input.compensation;
  const lengthSamples = piece.to - piece.from;
  return {
    passIndex: piece.pass,
    partial: isPartial(piece, input),
    startSample: piece.song,
    lengthSamples,
    firstFrame,
    channels: input.capture.read(firstFrame, lengthSamples),
    dropouts: input.capture.gapsIn(firstFrame, firstFrame + lengthSamples),
  };
}

function kept(piece: Piece, input: CaptureMapInput): boolean {
  const length = piece.to - piece.from;
  return length > 0 && (!isPartial(piece, input) || input.loop === null || length >= MIN_PARTIAL_PASS_SAMPLES);
}

/**
 * @param input - Capture, segments, loop, compensation, stop frame and track length.
 * @returns The takes of the session in capture order (no take without captured frames).
 */
export function mapCaptureToTakes(input: CaptureMapInput): MappedTake[] {
  if (input.capture.empty) {
    return [];
  }
  return piecesOf(input)
    .map((piece) => clip(piece, input))
    .filter((piece) => kept(piece, input))
    .map((piece) => toTake(piece, input));
}
