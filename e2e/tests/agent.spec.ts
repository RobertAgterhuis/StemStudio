import { freshTrack } from '../support/agent-flow';
import { finalText } from '../support/agent-scripts';
import { expect, test } from '../support/agent-test';
import { isOn } from '../support/controls';
import { fakeStems } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';

// Feature 011 composer agent (T040, T049, T058, T067, T074, T078b; TEST-STRATEGY 17.4). Run by
// scripts/agent-e2e.{sh,ps1}: one api start per script `e2e/agent-scripts/<name>.json` for the
// tests tagged `@<name>`; `@agent-unavailable` with backend `none`; `npm test` excludes `@agent`.
// Scripts address the test's own track or mix through `{{context.trackId}}` / `{{context.mixId}}`.

test.beforeEach(() => {
  test.setTimeout(3 * fakeJobTimeoutMs);
});

test('command executes: plan, steps, mute drums, answer', { tag: ['@agent', '@us1-command'] }, async ({
  api,
  agent,
  trackPage,
  mixer,
  page,
}, testInfo) => {
  const track = await freshTrack(api, `us1-${testInfo.project.name}`);
  await trackPage.goto(track.id);
  await agent.openNewConversation();
  await expect(agent.badge).toHaveAttribute('data-backend', 'fake');

  await agent.sendCommand('Find my agent track, open the mixer and mute the drums');
  await agent.expectAnswer(finalText('us1-command'));

  await agent.expectPlan(3, 'done');
  for (const tool of ['search_library', 'open_mixer', 'mixer_set_channel']) {
    await agent.expectStep(tool, 'succeeded');
  }
  await expect(page).toHaveURL(new RegExp(`/tracks/${track.id}/mixer`));
  await mixer.waitForReady(fakeStems);
  await expect.poll(() => isOn(mixer.mute('drums'))).toBe(true);
  await expect.poll(() => isOn(mixer.mute('bass'))).toBe(false);
  // US1-2: nothing is saved until the user saves.
  await expect(mixer.byId('unsaved-indicator')).toBeVisible();
  expect(await api.listMixes(track.id)).toHaveLength(0);
});
