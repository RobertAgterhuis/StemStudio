/**
 * The loopback calibration run (contract 2.5.0 `calibrateLatency`, research R-04 steps 1-4): plays the seeded chirp
 * clicks on the destination (directly, like the metronome), captures the input from `t0` to the last click plus
 * the tail, and hands the window to {@link estimateLatency}. Every click time is computed from `currentTime`; the
 * poll timer only waits for the capture.
 *
 * @packageDocumentation
 */
import type { CalibrationOptions, CalibrationResult } from '../types';
import type { CaptureReceiver } from './capture';
import { CAPTURE_BATCH_FRAMES } from './capture-worklet';
import { CALIBRATION_TAIL_SECONDS, chirpTemplate, clickSchedule, estimateLatency, validateCalibrationOptions } from './calibration';

/** Delay between the call and `t0`, the start of the capture window (s). */
export const CALIBRATION_LEAD_SECONDS = 0.1;
/** Poll interval of the capture wait (ms). */
const POLL_MS = 20;
/** Extra wall time the wait allows (ms). */
const SLACK_MS = 2000;

/** What a run needs. */
export interface CalibrationHost {
  readonly context: AudioContext;
  readonly capture: CaptureReceiver;
  /** Extra destinations of the clicks (the E2E loopback input). */
  readonly clickOutputs: () => readonly AudioNode[];
  /** Output latency snapshot `round((baseLatency + outputLatency) * 44100)`. */
  readonly outputLatencySamples: () => number;
}

/** @returns A seed for the click gaps (the schedule only has to be irregular). */
function randomSeed(): number {
  return Math.floor(Math.random() * 2 ** 31);
}

function playClicks(host: CalibrationHost, template: Float32Array, times: readonly number[]): void {
  const { context } = host;
  const buffer = context.createBuffer(1, template.length, context.sampleRate);
  buffer.getChannelData(0).set(template);
  times.forEach((time) => {
    const source = context.createBufferSource();
    source.buffer = buffer;
    [context.destination, ...host.clickOutputs()].forEach((output) => source.connect(output));
    source.start(time);
  });
}

async function waitFor(capture: CaptureReceiver, frame: number, context: AudioContext): Promise<void> {
  const expectedMs = ((frame - context.currentTime * context.sampleRate + CAPTURE_BATCH_FRAMES) / context.sampleRate) * 1000;
  const polls = Math.ceil((Math.max(0, expectedMs) + SLACK_MS) / POLL_MS);
  for (let i = 0; i < polls && capture.endFrame < frame; i++) {
    await new Promise((resolve) => setTimeout(resolve, POLL_MS));
  }
}

/**
 * Runs one calibration over the open input (the caller checked the preconditions and mutes mix and monitoring).
 *
 * @param host - Context, capture and click outputs.
 * @param options - Clicks and seed.
 * @returns The result; `ok: false` with a reason for a failed measurement.
 */
export async function runCalibration(host: CalibrationHost, options: CalibrationOptions | undefined): Promise<CalibrationResult> {
  const plan = validateCalibrationOptions(options);
  const { context, capture } = host;
  const rate = context.sampleRate;
  const template = chirpTemplate(rate);
  const clicks = clickSchedule(plan.clicks, plan.seed ?? randomSeed(), rate);
  const t0 = context.currentTime + CALIBRATION_LEAD_SECONDS;
  const t0Frame = Math.round(t0 * rate);
  const length = (clicks.at(-1) ?? 0) + Math.round(CALIBRATION_TAIL_SECONDS * rate);
  capture.start(length / rate + 1);
  playClicks(host, template, clicks.map((click) => t0 + click / rate));
  await waitFor(capture, t0Frame + length, context);
  capture.stop();
  const [window = new Float32Array(length)] = capture.take().read(t0Frame, length);
  return { ...estimateLatency(window, clicks, template, rate), outputLatencySamples: host.outputLatencySamples() };
}
