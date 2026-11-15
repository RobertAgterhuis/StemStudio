import { describe, expect, it } from 'vitest';
import { RAMP_TIME_CONSTANT } from '../src/channel';
import { SWAP_DELAY_SECONDS, StemGroups } from '../src/groups';
import { createEngine, type AudioEngine, type DetailGroup, type LoadTrackRequest } from '../src/index';
import { MockAudioContext, type MockAudioBufferSourceNode, type MockGainNode } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';
import { FetchStub } from './mocks/fetch';
import { settings } from './mocks/mixer-rig';

/** Encoded size per file; the mock buffer remembers it, so sources can be told apart. */
const BYTES = { vocals: 100, drums: 200, kick: 301, snare: 302, rest: 303 } as const;
type Part = keyof typeof BYTES;
const DETAILS: readonly Part[] = ['kick', 'snare', 'rest'];

const DRUMS: DetailGroup = {
  parent: 'drums',
  stems: [
    { name: 'kick', url: '/d/kick' },
    { name: 'snare', url: '/d/snare' },
    { name: 'rest', url: '/d/rest' },
  ],
};

const REQUEST: LoadTrackRequest = {
  stems: [
    { name: 'vocals', url: '/vocals' },
    { name: 'drums', url: '/drums' },
  ],
  beatGrid: null,
  groups: [DRUMS],
};

interface Rig {
  readonly ctx: MockAudioContext;
  readonly stub: FetchStub;
  readonly engine: AudioEngine;
}

function makeRig(): Rig {
  const ctx = new MockAudioContext();
  Object.values(BYTES).forEach((bytes) => ctx.decodeDurations.set(bytes, 20));
  const stub = new FetchStub()
    .reply('/vocals', { bytes: BYTES.vocals })
    .reply('/drums', { bytes: BYTES.drums })
    .reply('/d/kick', { bytes: BYTES.kick })
    .reply('/d/snare', { bytes: BYTES.snare })
    .reply('/d/rest', { bytes: BYTES.rest });
  const engine = createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch });
  return { ctx, stub, engine };
}

async function loaded(request: LoadTrackRequest = REQUEST): Promise<Rig> {
  const rig = makeRig();
  await rig.engine.loadTrack(request);
  return rig;
}

function partOf(source: MockAudioBufferSourceNode): Part | undefined {
  return (Object.keys(BYTES) as Part[]).find((p) => BYTES[p] === source.buffer?.sourceBytes);
}

/** Latest started source of a part. */
function sourceOf(ctx: MockAudioContext, part: Part): MockAudioBufferSourceNode {
  const source = ctx.startedSources().filter((s) => partOf(s) === part).at(-1);
  if (source === undefined) {
    throw new Error(`no source for ${part}`);
  }
  return source;
}

function gainOf(ctx: MockAudioContext, part: Part): MockGainNode {
  return sourceOf(ctx, part).connections[0] as MockGainNode;
}

/** Sources created since index `from`, by part. */
function startsSince(ctx: MockAudioContext, from: number): Record<string, unknown> {
  return Object.fromEntries(ctx.sources.slice(from).map((s) => [String(partOf(s)), s.startCalls]));
}

describe('loadTrack with groups', () => {
  it('creates the detail channels without fetching the detail files', async () => {
    const { engine, stub } = await loaded();
    expect(stub.requested).toEqual(['/vocals', '/drums']);
    expect(() => {
      engine.setChannel('drums.kick', settings({ volume: 0.5 }));
      engine.setChannel('drums.rest', settings());
    }).not.toThrow();
    expect(() => {
      engine.setChannel('drums.toms', settings());
    }).toThrow('Unknown stem "drums.toms"');
    expect(() => {
      engine.setChannel('vocals.lead', settings());
    }).toThrow('Unknown stem "vocals.lead"');
  });

  it('defaults to no groups', async () => {
    const { engine } = await loaded({ stems: REQUEST.stems, beatGrid: null });
    expect(() => {
      engine.setChannel('drums.kick', settings());
    }).toThrow('Unknown stem "drums.kick"');
    await expect(engine.setExpanded('drums', true)).rejects.toThrow('No detail group for "drums"');
  });

  it('rejects groups of unknown or unsplittable parents, duplicate groups and duplicate detail stems before fetching', async () => {
    const rig = makeRig();
    const bad: DetailGroup[][] = [
      [{ ...DRUMS, parent: 'piano' }],
      [{ ...DRUMS, parent: 'bass' as DetailGroup['parent'] }],
      [DRUMS, DRUMS],
      [{ parent: 'drums', stems: [{ name: 'kick', url: '/d/kick' }, { name: 'kick', url: '/d/kick2' }] }],
    ];
    const messages = ['Unknown group parent "piano"', 'Unknown group parent "bass"', 'Duplicate group "drums"', 'Duplicate stem "drums.kick"'];
    for (const [i, groups] of bad.entries()) {
      await expect(rig.engine.loadTrack({ ...REQUEST, groups })).rejects.toThrow(messages[i]);
    }
    expect(rig.stub.requested).toEqual([]);
  });

  it('attachMeter accepts detail channel ids', async () => {
    const { engine } = await loaded();
    expect(() => {
      engine.attachMeter('drums.snare', 'meter-drums-snare');
    }).not.toThrow();
    expect(() => {
      engine.attachMeter('drums.toms', 'meter-drums-toms');
    }).toThrow('Unknown meter target "drums.toms"');
    engine.detachMeter('drums.snare');
  });
});

describe('setExpanded', () => {
  it('rejects a parent without a group', async () => {
    const { engine } = await loaded();
    await expect(engine.setExpanded('vocals', true)).rejects.toThrow('No detail group for "vocals"');
    await expect(engine.setExpanded('vocals', false)).rejects.toThrow('No detail group for "vocals"');
  });

  it('fetches and decodes the group once, on the first expansion', async () => {
    const { engine, stub, ctx } = await loaded();
    await engine.setExpanded('drums', true);
    await engine.setExpanded('drums', false);
    await engine.setExpanded('drums', true);
    expect(stub.requested).toEqual(['/vocals', '/drums', '/d/kick', '/d/snare', '/d/rest']);
    expect(ctx.decoded).toHaveLength(5);
  });

  it('concurrent expansions share one download', async () => {
    const { engine, stub } = await loaded();
    await Promise.all([engine.setExpanded('drums', true), engine.setExpanded('drums', true)]);
    expect(stub.requested.filter((u) => u.startsWith('/d/'))).toHaveLength(3);
  });

  it('while playing, starts the group on the shared anchor and swaps all gates at one time', async () => {
    const { engine, ctx } = await loaded();
    ctx.currentTime = 2;
    await engine.play();
    ctx.currentTime = 3;
    const from = ctx.sources.length;
    await engine.setExpanded('drums', true);
    const t = 3 + SWAP_DELAY_SECONDS;
    const start = [{ when: t, offset: t - 2.1 }];
    expect(startsSince(ctx, from)).toEqual({ kick: start, snare: start, rest: start });
    const swap = (value: number): unknown[] => [value, t, RAMP_TIME_CONSTANT];
    expect(gainOf(ctx, 'drums').gain.calls.at(-1)?.args).toEqual(swap(0));
    expect(gainOf(ctx, 'vocals').gain.calls.at(-1)?.args).toEqual(swap(1));
    DETAILS.forEach((part) => {
      expect(gainOf(ctx, part).gain.calls.at(-1)?.args).toEqual(swap(1));
    });
  });

  it('collapsing swaps back at one time without downloading anything', async () => {
    const { engine, ctx, stub } = await loaded();
    await engine.play();
    await engine.setExpanded('drums', true);
    ctx.currentTime = 5;
    const requests = stub.requested.length;
    await engine.setExpanded('drums', false);
    const t = 5 + SWAP_DELAY_SECONDS;
    expect(stub.requested).toHaveLength(requests);
    expect(gainOf(ctx, 'drums').gain.calls.at(-1)?.args).toEqual([1, t, RAMP_TIME_CONSTANT]);
    expect(gainOf(ctx, 'kick').gain.calls.at(-1)?.args).toEqual([0, t, RAMP_TIME_CONSTANT]);
  });

  it('expanding right after play starts the group at the future shared when', async () => {
    const { engine, ctx } = await loaded();
    ctx.currentTime = 2;
    await engine.play();
    await engine.setExpanded('drums', true);
    expect(sourceOf(ctx, 'kick').startCalls).toEqual([{ when: 2.1, offset: 0 }]);
    expect(gainOf(ctx, 'drums').gain.calls.at(-1)?.args).toEqual([0, 2.1, RAMP_TIME_CONSTANT]);
  });

  it('inside a running loop the group starts at the wrapped offset with the loop properties', async () => {
    const { engine, ctx } = await loaded();
    engine.setLoop({ start: 1, end: 3, enabled: true });
    await engine.play();
    ctx.currentTime = 4.59;
    await engine.setExpanded('drums', true);
    const kick = sourceOf(ctx, 'kick');
    expect(kick.startCalls[0]?.when).toBeCloseTo(4.6, 9);
    expect(kick.startCalls[0]?.offset).toBeCloseTo(1.5, 9);
    expect([kick.loop, kick.loopStart, kick.loopEnd]).toEqual([true, 1, 3]);
  });

  it('expanding while stopped only loads; play then starts all sources sample-locked', async () => {
    const { engine, ctx } = await loaded();
    await engine.setExpanded('drums', true);
    expect(ctx.startedSources()).toEqual([]);
    ctx.currentTime = 1;
    await engine.play();
    const calls = ctx.startedSources().map((s) => s.startCalls);
    expect(calls).toHaveLength(5);
    expect(new Set(calls.map((c) => JSON.stringify(c))).size).toBe(1);
    expect(gainOf(ctx, 'drums').gain.value).toBe(0);
    expect(gainOf(ctx, 'snare').gain.value).toBe(1);
  });

  it('seek and loop changes treat detail sources like standard sources', async () => {
    const { engine, ctx } = await loaded();
    await engine.play();
    await engine.setExpanded('drums', true);
    ctx.currentTime = 2;
    const from = ctx.sources.length;
    engine.seek(5);
    const seekStart = [{ when: 2.1, offset: 5 }];
    expect(startsSince(ctx, from)).toEqual({ vocals: seekStart, drums: seekStart, kick: seekStart, snare: seekStart, rest: seekStart });
    engine.setLoop({ start: 4, end: 8, enabled: true });
    expect(ctx.sources.slice(from).every((s) => s.loop && s.loopStart === 4 && s.loopEnd === 8)).toBe(true);
  });

  it('pause, play and stop include detail sources', async () => {
    const { engine, ctx } = await loaded();
    await engine.play();
    await engine.setExpanded('drums', true);
    engine.pause();
    expect(ctx.sources.every((s) => s.stopCalls.length === 1)).toBe(true);
    const from = ctx.sources.length;
    await engine.play();
    expect(ctx.sources.slice(from)).toHaveLength(5);
    engine.stop();
    expect(ctx.sources.every((s) => s.stopCalls.length === 1)).toBe(true);
  });

  it('a failing group load rejects naming the stem and keeps the parent audible; a retry downloads again', async () => {
    const { engine, ctx, stub } = await loaded();
    stub.reply('/d/snare', { status: 404 });
    await engine.play();
    await expect(engine.setExpanded('drums', true)).rejects.toThrow('Failed to load stem "drums.snare": HTTP 404');
    expect(gainOf(ctx, 'drums').gain.value).toBe(1);
    expect(ctx.startedSources()).toHaveLength(2);
    stub.reply('/d/snare', { bytes: BYTES.snare });
    await engine.setExpanded('drums', true);
    expect(gainOf(ctx, 'drums').gain.value).toBe(0);
    expect(stub.requested.filter((u) => u === '/d/snare')).toHaveLength(2);
  });

  it('collapsing while the group loads keeps the parent audible once the load finishes', async () => {
    const { engine, ctx, stub } = await loaded();
    await engine.play();
    const release = stub.hold('/d/');
    const expanding = engine.setExpanded('drums', true);
    await engine.setExpanded('drums', false);
    release();
    await expanding;
    expect(gainOf(ctx, 'drums').gain.value).toBe(1);
    expect(gainOf(ctx, 'kick').gain.value).toBe(0);
  });

  it('a new track while the group loads rejects the expansion and drops the stale group', async () => {
    const { engine, ctx, stub } = await loaded();
    const release = stub.hold('/d/');
    const expanding = engine.setExpanded('drums', true);
    await engine.loadTrack(REQUEST);
    release();
    await expect(expanding).rejects.toThrow('Track changed while loading the detail stems of "drums"');
    await engine.play();
    expect(ctx.startedSources()).toHaveLength(2);
  });

  it('a new track collapses every parent and drops the decoded groups', async () => {
    const { engine, ctx, stub } = await loaded();
    await engine.setExpanded('drums', true);
    await engine.loadTrack(REQUEST);
    await engine.play();
    expect(ctx.startedSources()).toHaveLength(2);
    expect(gainOf(ctx, 'drums').gain.value).toBe(1);
    await engine.setExpanded('drums', true);
    expect(stub.requested.filter((u) => u === '/d/kick')).toHaveLength(2);
  });

  it('rejects after dispose, also for a load still in flight', async () => {
    const { engine, stub } = await loaded();
    const release = stub.hold('/d/');
    const expanding = engine.setExpanded('drums', true);
    await engine.dispose();
    release();
    await expect(expanding).rejects.toThrow('Audio engine has been disposed');
    await expect(engine.setExpanded('drums', false)).rejects.toThrow('Audio engine has been disposed');
  });
});

/** Track time a browser plays from a source at context time `time` (loop semantics of Web Audio). */
function playhead(source: MockAudioBufferSourceNode, time: number): number {
  const start = source.startCalls[0];
  if (start === undefined) {
    throw new Error('source not started');
  }
  const raw = (start.offset ?? 0) + (time - start.when);
  if (!source.loop || raw < source.loopEnd) {
    return raw;
  }
  return source.loopStart + ((raw - source.loopStart) % (source.loopEnd - source.loopStart));
}

describe('sample lock with groups (SC-004)', () => {
  const LOOP = { start: 1.37, end: 3.91, enabled: true };
  const LENGTH = LOOP.end - LOOP.start;

  async function expandedMidLoop(): Promise<Rig & { t: number }> {
    const rig = await loaded();
    rig.engine.setLoop(LOOP);
    rig.ctx.currentTime = 0.123;
    await rig.engine.play();
    rig.ctx.currentTime = 0.123 + 7.777;
    await rig.engine.setExpanded('drums', true);
    return { ...rig, t: rig.ctx.currentTime + SWAP_DELAY_SECONDS };
  }

  it('after 50 loop repetitions every audible channel plays the same track time within 1 ms', async () => {
    const { engine, ctx, t } = await expandedMidLoop();
    const audible: Part[] = ['vocals', ...DETAILS];
    for (let k = 1; k <= 50; k += 1) {
      const time = t + k * LENGTH + 0.3;
      ctx.currentTime = time;
      const heads = audible.map((part) => playhead(sourceOf(ctx, part), time));
      expect(Math.max(...heads) - Math.min(...heads)).toBeLessThan(0.001);
      expect(Math.abs((heads[0] ?? 0) - engine.getPosition())).toBeLessThan(0.001);
      expect(Math.abs(playhead(sourceOf(ctx, 'drums'), time) - engine.getPosition())).toBeLessThan(0.001);
    }
  });

  it('collapsing and re-expanding during the repetitions only moves gates, never restarts sources', async () => {
    const { engine, ctx, t } = await expandedMidLoop();
    const sources = ctx.sources.length;
    ctx.currentTime = t + 20 * LENGTH;
    await engine.setExpanded('drums', false);
    ctx.currentTime = t + 30 * LENGTH;
    await engine.setExpanded('drums', true);
    expect(ctx.sources).toHaveLength(sources);
    expect(ctx.sources.every((s) => s.stopCalls.length === 0)).toBe(true);
  });

  it('a seek inside the loop after the expansion restarts all channels on one fresh anchor', async () => {
    const { engine, ctx, t } = await expandedMidLoop();
    ctx.currentTime = t + 25 * LENGTH;
    const from = ctx.sources.length;
    engine.seek(2);
    const started = ctx.sources.slice(from);
    expect(started).toHaveLength(5);
    expect(new Set(started.map((s) => JSON.stringify(s.startCalls))).size).toBe(1);
    const time = ctx.currentTime + 25 * LENGTH;
    const heads = started.map((s) => playhead(s, time));
    expect(Math.max(...heads) - Math.min(...heads)).toBeLessThan(0.001);
  });
});

describe('memory release on dispose (T085)', () => {
  it('disconnects every standard and detail channel', async () => {
    const { engine, ctx } = await loaded();
    await engine.play();
    await engine.setExpanded('drums', true);
    const gains = ctx.startedSources().map((s) => s.connections[0] as MockGainNode);
    expect(gains).toHaveLength(5);
    await engine.dispose();
    expect(gains.every((g) => g.disconnected)).toBe(true);
    expect(ctx.panners.every((p) => p.disconnected)).toBe(true);
  });

  it('StemGroups.dispose forgets every group, loaded buffer and pending load', async () => {
    const attached: number[] = [];
    const host = {
      load: () => Promise.resolve(new Map()),
      swapTime: () => 1,
      attach: (_buffers: unknown, at: number) => {
        attached.push(at);
      },
      swap: () => undefined,
    };
    const groups = new StemGroups(host);
    groups.reset([DRUMS]);
    const pending = groups.setExpanded('drums', true);
    groups.dispose();
    await expect(pending).rejects.toThrow('Track changed while loading the detail stems of "drums"');
    await expect(groups.setExpanded('drums', true)).rejects.toThrow('No detail group for "drums"');
    expect(attached).toEqual([]);
  });
});
