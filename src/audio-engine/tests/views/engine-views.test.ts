import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import { makeEngineRig, loadVocals } from '../mocks/engine-rig';
import { practice } from '../mocks/practice-rig';
import { GRID, addHost, cycleChart } from '../mocks/views-rig';

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('engine chord lane (2.6.0)', () => {
  it('attaches a lane with the loaded beat grid, follows seeks and detaches', async () => {
    const rig = makeEngineRig();
    await loadVocals(rig, GRID);
    const host = addHost('lane');
    rig.engine.attachChordLane('lane', cycleChart(), { reducedMotion: false });
    rig.step();
    expect(host.querySelectorAll('.chord-lane-item')).toHaveLength(8);
    rig.engine.seek(10);
    rig.step();
    expect(host.dataset.positionMs).toBe('10000');
    expect(host.dataset.currentSymbol).toBe('C');
    rig.engine.detachChordLane();
    expect(host.children).toHaveLength(0);
  });

  it('redraws the lane after a loop change and re-spells it after a transpose change', async () => {
    const rig = makeEngineRig({ timeStretchFactory: (context) => Promise.resolve(new FakeStretchStage(context)) });
    await loadVocals(rig, GRID);
    const host = addHost('lane');
    rig.engine.attachChordLane('lane', cycleChart(), { reducedMotion: false });
    rig.step();
    rig.engine.setLoop({ start: 1, end: 5, enabled: true });
    rig.step();
    expect(host.dataset.positionMs).toBe('0');
    expect(host.dataset.nextSymbol).toBe('C');
    await rig.engine.setPractice(practice({ transposeSemitones: 2 }));
    rig.step();
    expect(host.dataset.nextSymbol).toBe('D');
    expect(host.querySelectorAll('.chord-lane-item')[1]?.textContent).toBe('D');
  });
});
