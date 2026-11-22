import { InstrumentViews } from '../pages/instrument-views';
import { isOn } from '../support/controls';
import { ensureDetailCompleted, fakeStems } from '../support/detail';
import { families, familyView } from '../support/instrument-family';
import { drumDetailKeys, expectFamilyMuted, setAllExpanded } from '../support/instrument-mixer';
import { expect, test } from '../support/test';

// Feature 016 US6 (T115; FR-057..FR-060, SC-006, SC-009, DELTA 6): "Practise as ..." for every family on the fake stack.
// The band fixture with a completed detail phase has split vocals, drums and other, so the family rule (data-model 5.3)
// is checked with the groups collapsed (standard strips) and expanded (detail strips). Every family has its own test so
// that each one counts on its own (instrument-neutral, SC-006).

test.describe('Practise as ... (016 US6)', { tag: '@instrument' }, () => {
  for (const family of families) {
    test(`${family}: mutes its family, unmutes the rest and opens its view`, async ({ api, mixer, page }) => {
      const track = await ensureDetailCompleted(api);
      const ui = new InstrumentViews(page);
      await mixer.goto(track.id);
      await mixer.waitForReady(fakeStems);

      await ui.practiseAsFamily(family);

      await expect(ui.view).toHaveAttribute('data-view', familyView[family]);
      await expect(ui.byId(`practise-as-${family}`)).toHaveAttribute('aria-checked', 'true');
      await expectFamilyMuted(mixer, track, family);
    });
  }

  test('the chosen family is offered again after a reload, not applied', async ({ api, mixer, page }) => {
    const track = await ensureDetailCompleted(api);
    const ui = new InstrumentViews(page);
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await ui.practiseAsFamily('bass');

    await page.reload();
    await mixer.waitForReady(fakeStems);

    await expect(ui.byId('practise-as-offer')).toBeVisible();
    await expect(ui.practiseAs).toHaveAttribute('data-family', '');
    await expect.poll(() => isOn(mixer.mute('bass'))).toBe(false);
  });
});

test.describe('004 presets follow the family rule (016 DELTA 6)', { tag: '@instrument' }, () => {
  test('play-along-drums with expanded drums mutes every drum detail strip', async ({ api, mixer }) => {
    const track = await ensureDetailCompleted(api);
    await mixer.goto(track.id);
    await mixer.waitForReady(fakeStems);
    await setAllExpanded(mixer, track, true);

    await mixer.applyPreset('play-along-drums');

    for (const key of drumDetailKeys(track)) {
      await expect.poll(() => isOn(mixer.mute(key)), { message: `mute-${key}` }).toBe(true);
    }
    await expectFamilyMuted(mixer, track, 'drums');
  });
});
