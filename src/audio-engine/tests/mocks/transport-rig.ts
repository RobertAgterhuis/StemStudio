import { Transport } from '../../src/transport';
import type { StemName } from '../../src/types';
import { MockAudioContext } from './audio-context';

/** A transport over one 60 s stem on a fresh mock context (start delay 0.1 s). */
export interface TransportRig {
  readonly ctx: MockAudioContext;
  readonly transport: Transport;
}

/** Builds a {@link TransportRig}; `seconds` is the stem length. */
export function makeTransport(seconds = 60): TransportRig {
  const ctx = new MockAudioContext();
  const transport = new Transport({
    context: ctx.asAudioContext(),
    startDelaySeconds: 0.1,
    output: () => ctx.createGain() as unknown as AudioNode,
  });
  const buffer = ctx.createBuffer(2, seconds * ctx.sampleRate, ctx.sampleRate) as unknown as AudioBuffer;
  transport.load(new Map<StemName, AudioBuffer>([['vocals', buffer]]));
  return { ctx, transport };
}
