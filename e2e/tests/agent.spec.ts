import { ensureBackendNone, freshTrack, freshTrackWithoutDetail, injectionTrack, openMixer, savedMix } from '../support/agent-flow';
import { finalText } from '../support/agent-scripts';
import { expect, test } from '../support/agent-test';
import { isOn } from '../support/controls';
import { fakeStems, silentDb } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs, runId } from '../support/env';
import { oneOf } from '../support/text';

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

test('confirm: delete mix runs only after Approve', { tag: ['@agent', '@us2-delete-mix'] }, async ({
  api,
  agent,
  agentApi,
  mixer,
}, testInfo) => {
  const track = await freshTrack(api, `us2-mix-${testInfo.project.name}`);
  const mix = await savedMix(api, track.id, `Agent delete ${runId()} ${testInfo.project.name}`);
  await openMixer(mixer, track.id, mix.id);
  await agent.openNewConversation();

  await agent.sendCommand('Delete this mix');
  await agent.expectApproval('delete_mix', 'destructive');
  expect(await agent.approvalReasons()).toContain('risk-destructive');
  await expect(agent.byId('agent-approval-arguments')).toContainText(mix.id);
  await expect(agent.byId('agent-approve-plan')).toHaveCount(0);
  expect(await agentApi.statusOf(`/api/mixes/${mix.id}`), 'nothing runs before Approve').toBe(200);

  await agent.approve();
  await agent.expectAnswer(finalText('us2-delete-mix'));
  await agent.expectStep('delete_mix', 'succeeded');
  expect(await agentApi.statusOf(`/api/mixes/${mix.id}`)).toBe(404);
});

test('reject: a rejected delete keeps the track', { tag: ['@agent', '@us2-delete-track'] }, async ({
  api,
  agent,
  agentApi,
  trackPage,
  trackList,
  page,
}, testInfo) => {
  const track = await freshTrack(api, `us2-track-${testInfo.project.name}`);
  await trackPage.goto(track.id);
  await agent.openNewConversation();

  await agent.sendCommand('Verwijder deze track');
  const card = await agent.expectApproval('delete_track', 'destructive');
  // US2-1: the card names the track and says that it cannot be undone.
  await expect(card).toContainText(track.title);
  await expect(agent.byId('agent-approval-effect')).toContainText(/cannot be undone/i);
  expect(await agentApi.statusOf(`/api/tracks/${track.id}`)).toBe(200);

  await agent.reject();
  await agent.expectAnswer(finalText('us2-delete-track'));
  await agent.expectStep('delete_track', 'rejected');
  expect(await agentApi.statusOf(`/api/tracks/${track.id}`)).toBe(200);
  await page.goto('/tracks');
  await expect(trackList.row(track.id)).toBeVisible();
});

test('confirm: an approved split further queues the detail phase', { tag: ['@agent', '@us2-detail'] }, async ({
  api,
  agent,
  trackPage,
}, testInfo) => {
  test.setTimeout(3 * fakeJobTimeoutMs + detailPhaseTimeoutMs);
  const track = await freshTrackWithoutDetail(api, `us2-detail-${testInfo.project.name}`);
  const before = track.detail.status;
  await trackPage.goto(track.id);
  await agent.openNewConversation();

  await agent.sendCommand('Split this track further');
  await agent.expectApproval('start_detail_separation', 'heavy');
  expect(await agent.approvalReasons()).toContain('risk-heavy');
  const estimate = agent.byId('agent-approval-estimate');
  await expect(estimate).toHaveAttribute('data-device', /^(gpu|cpu)$/);
  expect(Number(await estimate.getAttribute('data-minutes'))).toBeGreaterThan(0);
  expect((await api.getTrack(track.id)).detail.status, 'nothing runs before Approve').toBe(before);

  await agent.approve();
  await agent.expectAnswer(finalText('us2-detail'));
  await agent.expectStep('start_detail_separation', 'succeeded');
  const status = (await api.getTrack(track.id)).detail.status;
  expect(status).toMatch(oneOf(['queued', 'running', 'completed']));
});

test('injection: a delete proposed after reading the title is held as tainted', { tag: ['@agent', '@us2-inject'] }, async ({
  api,
  agent,
  agentApi,
  trackPage,
}, testInfo) => {
  const track = await injectionTrack(api, agentApi, `${runId()}-${testInfo.project.name}`);
  const tracksBefore = (await api.listTracks()).length;
  await trackPage.goto(track.id);
  await agent.openNewConversation();

  await agent.sendCommand('Wat staat er in de titel van deze track?');
  await agent.expectStep('get_track', 'succeeded');
  await agent.expectApproval('delete_track', 'destructive');
  expect(await agent.approvalReasons()).toContain('tainted');

  await agent.reject();
  await agent.expectAnswer(finalText('us2-inject'));
  await agent.expectStep('delete_track', 'rejected');
  const after = await api.getTrack(track.id);
  expect(after.status).toBe('completed');
  expect(after.title).toBe(track.title);
  expect(await api.listTracks()).toHaveLength(tracksBefore);
});

test('unavailable: backend none shows the reason and the rest of the app works', { tag: ['@agent', '@agent-unavailable'] }, async ({
  api,
  agent,
  agentApi,
  mixer,
  page,
}, testInfo) => {
  await ensureBackendNone(agentApi);
  const started = Date.now();
  const status = await agentApi.status();
  expect(Date.now() - started, 'GET /api/agent/status within 3 s (US3-1)').toBeLessThanOrEqual(3_000);
  expect(status.ready).toBe(false);

  await page.goto('/');
  await agent.open();
  await expect(agent.unavailable).toBeVisible({ timeout: 3_000 });
  await expect(agent.unavailable).not.toBeEmpty();
  await expect(agent.badge).toHaveAttribute('data-backend', 'none');
  await expect(agent.input).toBeDisabled();
  await expect(agent.send).toBeDisabled();

  const conversationId = await agentApi.createConversation(`Unavailable ${runId()}`);
  const refused = await agentApi.sendMessage(conversationId, 'Zoek Hero of the Day', 'import');
  expect(refused.status()).toBe(503);
  expect(((await refused.json()) as { type: string }).type).toMatch(/backend-unavailable$/);
  expect(await agentApi.deleteConversation(conversationId)).toBe(204);

  // SC-005: import, separation and the mixer work as before with the panel open.
  const track = await freshTrack(api, `us3-${testInfo.project.name}`);
  await openMixer(mixer, track.id);
  await agent.open();
  await expect(agent.unavailable).toBeVisible();
  await mixer.play();
  expect(await mixer.maxPeakOver('master', 2_000)).toBeGreaterThan(silentDb);
});
