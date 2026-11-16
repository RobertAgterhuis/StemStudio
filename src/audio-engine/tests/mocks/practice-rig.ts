import { createEngine } from '../../src/index';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import type { AudioEngine, BeatGrid, PracticeSettings, PracticeStatus, TimeStretchStage } from '../../src/types';
import { MockAudioContext, type MockAudioBufferSourceNode } from './audio-context';
import { FakeDotNetRef } from './dotnet-ref';
import { FetchStub } from './fetch';

/** Neutral practice settings (the defaults of `PracticeSettingsDto`). */
export const NEUTRAL: PracticeSettings = {
  speedPercent: 100,
  transposeSemitones: 0,
  trainer: { enabled: false, startPercent: 70, stepPercent: 5, targetPercent: 100, passesPerStep: 1 },
  countIn: { bars: 0, everyPass: false },
};

/** Practice settings with overrides (nested objects merged). */
export function practice(partial: {
  speedPercent?: number;
  transposeSemitones?: number;
  trainer?: Partial<PracticeSettings['trainer']>;
  countIn?: Partial<PracticeSettings['countIn']>;
}): PracticeSettings {
  return {
    ...NEUTRAL,
    ...partial,
    trainer: { ...NEUTRAL.trainer, ...partial.trainer },
    countIn: { ...NEUTRAL.countIn, ...partial.countIn },
  };
}

/** An engine over the mock context with a fake stretch stage factory. */
export interface PracticeRig {
  readonly ctx: MockAudioContext;
  readonly ref: FakeDotNetRef;
  readonly engine: AudioEngine;
  /** Stages created by the factory, in order. */
  readonly stages: FakeStretchStage[];
  /** Number of factory calls. */
  calls: number;
  /** When set, the next factory call rejects with this error. */
  failWith: Error | null;
  /** When set, factory calls wait for it. */
  gate: Promise<void> | null;
}

/** Builds a {@link PracticeRig}; `vocals` and `drums` stems (20 s), a drums group with `kick` and `rest`. */
export function makePracticeRig(): PracticeRig {
  const ctx = new MockAudioContext();
  ctx.decodeDurations.set(100, 20);
  const stub = new FetchStub();
  ['/vocals', '/drums', '/kick', '/rest'].forEach((url) => stub.reply(url, { bytes: 100 }));
  const ref = new FakeDotNetRef();
  const rig: PracticeRig = { ctx, ref, engine: undefined as unknown as AudioEngine, stages: [], calls: 0, failWith: null, gate: null };
  const factory = async (context: AudioContext): Promise<TimeStretchStage> => {
    rig.calls += 1;
    await rig.gate;
    if (rig.failWith !== null) {
      throw rig.failWith;
    }
    const stage = new FakeStretchStage(context);
    rig.stages.push(stage);
    return stage;
  };
  const engine = createEngine(ref, { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch, timeStretchFactory: factory });
  return Object.assign(rig, { engine });
}

/** A rig with the track loaded (and optionally a beat grid). */
export async function loadedPracticeRig(beatGrid: BeatGrid | null = null): Promise<PracticeRig> {
  const rig = makePracticeRig();
  await rig.engine.loadTrack({
    stems: [
      { name: 'vocals', url: '/vocals' },
      { name: 'drums', url: '/drums' },
    ],
    beatGrid,
    groups: [{ parent: 'drums', stems: [{ name: 'kick', url: '/kick' }, { name: 'rest', url: '/rest' }] }],
  });
  return rig;
}

/** The last stage, failing when none was created. */
export function stageOf(rig: PracticeRig): FakeStretchStage {
  const stage = rig.stages.at(-1);
  if (stage === undefined) {
    throw new Error('no stage');
  }
  return stage;
}

/** Track sources (not clicks) that are running (started, not stopped). */
export function running(rig: PracticeRig): MockAudioBufferSourceNode[] {
  return rig.ctx.sources.filter((s) => s.startCalls.length > 0 && s.stopCalls.length === 0 && s.buffer !== null && s.buffer.length > 44100);
}

/** `OnPracticeChanged` payloads so far. */
export function statuses(rig: PracticeRig): PracticeStatus[] {
  return rig.ref.argsOf('OnPracticeChanged') as PracticeStatus[];
}

/** `setValueAtTime` calls of a source's playbackRate. */
export function rateSteps(source: MockAudioBufferSourceNode): number[][] {
  return source.playbackRate.calls.filter((c) => c.method === 'setValueAtTime').map((c) => [...c.args]);
}
