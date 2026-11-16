import type { MixerPage } from '../pages/mixer-page';
import type { PracticePanel, TrainerInputs } from '../pages/practice-panel';
import type { TrackDto } from '../support/api';
import { installClickProbe, musicStartTimes, readClicks } from '../support/click-probe';
import { isOn } from '../support/controls';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { distinctValues, heardLatencySeconds, practiceWith, readWatched, watchAttributes } from '../support/practice';
import { expect, test } from '../support/test';

// Feature 012 US2 (T054; FR-010, FR-011, FR-014; SC-006; quickstart P8). Fake stack, the 12 s
// click fixture, level Advanced (the suite default). Loop 2.0-6.0 s (4 s of track time).
const loop = { start: 2, end: 6 } as const;
const sequence: TrainerInputs = { start: 60, step: 10, target: 80, passes: 1 };
let track: TrackDto;

test.beforeEach(async ({ api, mixer }) => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  track = await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs);
  await mixer.goto(track.id);
  await mixer.waitForReady(fakeStems);
});

async function armTrainer(mixer: MixerPage, practice: PracticePanel, inputs: TrainerInputs): Promise<void> {
  await mixer.setLoop(loop.start, loop.end, true);
  await practice.setTrainer(inputs, true);
  await expect(practice.trainerStatus).toHaveAttribute('data-state', 'waiting');
}

/** `pass:speed` of every recorded trainer status while it runs or holds the target. */
function passSpeeds(samples: Awaited<ReturnType<typeof readWatched>>): string[] {
  const playing = samples.filter((s) => ['running', 'target'].includes(s.values['data-state'] ?? ''));
  const keyed = playing.map((s) => ({ at: s.at, values: { key: `${s.values['data-pass'] ?? ''}:${s.values['data-speed'] ?? ''}` } }));
  return distinctValues(keyed, 'key');
}

test.describe('speed trainer', { tag: '@practice' }, () => {
  test('start 60, step 10, target 80, 1 pass per step plays 60, 70, 80, 80 at the pass boundaries (P8, US2-1, US2-2, SC-006)', async ({ mixer, page, practice }) => {
    await installClickProbe(page);
    await page.reload();
    await mixer.waitForReady(fakeStems);
    await armTrainer(mixer, practice, sequence);
    await watchAttributes(page, 'trainer-status', ['data-state', 'data-pass', 'data-speed']);
    const before = (await readClicks(page)).now;
    await mixer.play();
    await expect(practice.trainerStatus).toHaveAttribute('data-pass', '4', { timeout: 45_000 });

    expect(passSpeeds(await readWatched(page, 'trainer-status'))).toEqual(['1:60', '2:70', '3:80', '4:80']);
    await expect(practice.trainerStatus).toHaveAttribute('data-state', 'target');
    await expect(practice.panel).toHaveAttribute('data-speed', '80');
    await expect(practice.speed).toHaveAttribute('aria-readonly', 'true');
    // Pass k starts at T_k - L(p_k) and lasts (B - A) / r_k (audio-engine.md "Explicit passes").
    const starts = musicStartTimes(await readClicks(page), before).slice(0, 4);
    const speeds = [60, 70, 80, 80];
    const latency = (k: number): number => heardLatencySeconds(speeds[k] ?? 100, 0);
    const expected = [0, 1, 2].map((k) => (loop.end - loop.start) / ((speeds[k] ?? 100) / 100) - (latency(k + 1) - latency(k)));
    const actual = starts.slice(1).map((t, k) => t - (starts[k] ?? t));
    expect(actual).toHaveLength(3);
    test.info().annotations.push({ type: 'pass lengths s', description: JSON.stringify(actual) });
    actual.forEach((length, k) => {
      expect(Math.abs(length - (expected[k] ?? 0)), `pass ${String(k + 1)}`).toBeLessThanOrEqual(0.005);
    });
  });

});

test.describe('speed trainer controls', { tag: '@practice' }, () => {
  test('pause keeps pass and speed, transpose applies live, stop resets to pass 1 (P8, US2-3, US2-6)', async ({ mixer, practice }) => {
    await armTrainer(mixer, practice, sequence);
    await mixer.play();
    await expect(practice.trainerStatus).toHaveAttribute('data-pass', '2', { timeout: 20_000 });
    await practice.byId('practice-transpose-up').click();
    await expect(practice.panel).toHaveAttribute('data-transpose', '1');
    await expect(practice.trainerStatus).toHaveAttribute('data-pass', '2');
    await expect(practice.trainerStatus).toHaveAttribute('data-state', 'running');

    await mixer.byId('transport-pause').click();
    await expect(practice.trainerStatus).toHaveAttribute('data-pass', '2');
    await expect(practice.trainerStatus).toHaveAttribute('data-speed', '70');
    await mixer.play();
    await expect(practice.trainerStatus).toHaveAttribute('data-state', 'running');
    await expect(practice.trainerStatus).toHaveAttribute('data-speed', '70');

    await mixer.byId('transport-stop').click();
    await expect(practice.trainerStatus).toHaveAttribute('data-pass', '1');
    await expect(practice.trainerStatus).toHaveAttribute('data-speed', '60');
  });

  test('without a loop, or with one under 0.5 s, the trainer says why; a target at or below the start is refused (US2-4)', async ({ mixer, practice }) => {
    await expect(practice.byId('trainer-needs-loop')).toHaveText('Set a loop (A-B) to use the speed trainer.');
    await expect(practice.trainerToggle).toBeDisabled();
    await mixer.setLoop(2, 2.3, true);
    await expect(practice.byId('trainer-needs-loop')).toContainText('at least 0.5 s');
    await mixer.setLoop(loop.start, loop.end, true);
    await expect(practice.byId('trainer-needs-loop')).toHaveCount(0);
    await expect(practice.trainerToggle).toBeEnabled();
    await practice.setTrainer({ start: 80, step: 5, target: 80, passes: 1 }, false);
    await expect(practice.byId('trainer-error')).toBeVisible();
    await expect(practice.byId('trainer-error')).not.toBeEmpty();
  });

  test('trainer settings are saved in the mix and restored after a reload; progress is not (US2-5, FR-014)', async ({ api, mixer, mixLibrary, page, practice }, testInfo) => {
    const inputs: TrainerInputs = { start: 65, step: 5, target: 90, passes: 2 };
    await armTrainer(mixer, practice, inputs);
    const name = `Trainer ${runId()} ${testInfo.project.name}`;
    await mixer.saveNew(name);
    await expect.poll(async () => (await api.findMix(track.id, name))?.id).toBeTruthy();
    const id = (await api.findMix(track.id, name))?.id ?? '';
    const trainer = { enabled: true, startPercent: 65, stepPercent: 5, targetPercent: 90, passesPerStep: 2 };
    expect((await api.getMix(id)).settings.practice).toEqual(practiceWith({ trainer }));

    await page.reload();
    await mixLibrary.goto();
    await mixLibrary.load(id);
    await mixer.waitForReady(fakeStems);
    await practice.expectTrainerInputs(inputs);
    expect(await isOn(practice.trainerToggle)).toBe(true);
    await expect(practice.trainerStatus).toHaveAttribute('data-state', 'waiting');
    await expect(practice.trainerStatus).toHaveAttribute('data-speed', '65');
  });
});
