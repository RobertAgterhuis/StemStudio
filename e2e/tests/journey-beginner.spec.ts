import {
  chooseBeginnerInWelcome,
  importForJourney,
  openMixerFromTrack,
  playAlongDrums,
  saveJourneyMix,
  type JourneyContext,
} from '../support/journey';
import { uniqueMixName } from '../support/main-flow';
import { freshState } from '../support/preferences';
import { expect, test } from '../support/ux-test';

// T047 / SC-007, quickstart V-J1 + V-J2: an empty browser profile reaches a saved practice mix
// using only the welcome panel, the journey rail, primary buttons and a preset.
test.use({ storageState: freshState });

test(
  'a first-time Beginner goes from an empty app to a saved "Play along: drums" mix',
  { tag: '@ux' },
  async ({ page, api, shell, importPage, trackPage, mixer }, testInfo) => {
    const ctx: JourneyContext = { page, api, shell, importPage, trackPage, mixer };

    await test.step('welcome -> "I am new to this"', () => chooseBeginnerInWelcome(ctx));

    const trackId = await test.step('import the fixture and wait for the separation', () =>
      importForJourney(ctx, testInfo.project.name));

    await test.step('primary button: open the mixer', () => openMixerFromTrack(ctx, trackId));

    await test.step('preset: Play along: drums', () => playAlongDrums(ctx));

    await test.step('primary button: Save with a name', () =>
      saveJourneyMix(ctx, trackId, uniqueMixName('Drums practice', testInfo.project.name)));

    await expect(shell.journeyStep('save')).toHaveAttribute('data-state', 'done');
  },
);
