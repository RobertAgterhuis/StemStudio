import {
  ensureBackendNone,
  expectFaders,
  expectStoredBackend,
  freshTrack,
  faderValues,
  freshTrackWithoutDetail,
  injectionTrack,
  openMixer,
  restoreBackend,
  savedMix,
  storedBackendToRestore,
} from '../support/agent-flow';
import type { AgentPanel } from '../pages/agent-panel';
import { finalText } from '../support/agent-scripts';
import { expect, test } from '../support/agent-test';
import { isOn } from '../support/controls';
import { fakeStems, silentDb } from '../support/detail';
import { requireGeneratorMode } from '../support/generative';
import { sourceTrack } from '../support/generative-flow';
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
  await approveTaintedMute(agent);
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

test('settings: Claude is not selectable without a key; switch to none and back to Ollama', { tag: ['@agent'] }, async ({
  agentApi,
  settings,
  page,
}) => {
  const restore = await storedBackendToRestore(agentApi);
  try {
    await page.goto('/');
    await settings.openFromRail();
    // US4-1, SC-006: CI never sets ANTHROPIC_API_KEY; the page shows presence only, never a key.
    await expect(settings.claudeStatus).toHaveAttribute('data-key-configured', 'false');
    await expect(settings.claudeStatus).toContainText('ANTHROPIC_API_KEY');
    await expect(settings.backend('claude')).toBeDisabled();
    await expect(page.locator('body')).not.toContainText('sk-ant-');

    await settings.saveBackend('none');
    await expectStoredBackend(agentApi, 'none');
    await page.reload();
    await settings.expectSelected('none');

    await settings.saveBackend('ollama');
    await expectStoredBackend(agentApi, 'ollama');
    await page.reload();
    await settings.expectSelected('ollama');
    await expect(settings.byId('settings-agent-ollama-status')).toBeVisible();
    // The test-only fake backend of the environment stays effective (TEST-STRATEGY 17.4).
    expect((await agentApi.status()).backend).toBe('fake');
  } finally {
    await restoreBackend(agentApi, restore);
  }
});

const undoStems = ['drums', 'bass', 'vocals'] as const;

test('undo: three mixer changes return to their previous values', { tag: ['@agent', '@us5-mixer-undo'] }, async ({
  api,
  agent,
  mixer,
}, testInfo) => {
  const track = await freshTrack(api, `us5-undo-${testInfo.project.name}`);
  await openMixer(mixer, track.id);
  const before = await faderValues(mixer, undoStems);
  await agent.openNewConversation();

  await agent.sendCommand('Drums op -6 dB, bas op -12 dB en de zang 3 dB harder');
  await agent.expectAnswer(finalText('us5-mixer-undo'));
  await expect(agent.steps('mixer_set_channel')).toHaveCount(3);
  for (const step of await agent.steps('mixer_set_channel').all()) {
    await expect(step).toHaveAttribute('data-status', 'succeeded');
  }
  const changed = await faderValues(mixer, undoStems);
  changed.forEach((value, index) => {
    expect(value, undoStems[index]).not.toBeCloseTo(before[index] ?? 0, 0);
  });

  await expect(agent.undo).toBeEnabled();
  await agent.undo.click();
  await expectFaders(mixer, undoStems, before);
  await expect(agent.approvalCard).toHaveCount(0);
  await expect(agent.undo).toBeDisabled({ timeout: 30_000 });
});

test('undo: a mix saved by the agent is deleted again', { tag: ['@agent', '@us5-save-undo'] }, async ({
  api,
  agent,
  mixer,
}, testInfo) => {
  const track = await freshTrack(api, `us5-save-${testInfo.project.name}`);
  await openMixer(mixer, track.id);
  await agent.openNewConversation();

  await agent.sendCommand('Save this as Agent mix');
  await agent.expectAnswer(finalText('us5-save-undo'));
  await agent.expectStep('save_mix_as', 'succeeded');
  await expect.poll(async () => (await api.findMix(track.id, 'Agent mix'))?.id).toBeDefined();

  // US5-2: the agent's own fresh mix is deleted without a confirmation card.
  await agent.undo.click();
  await expect.poll(async () => api.findMix(track.id, 'Agent mix'), { timeout: 30_000 }).toBeUndefined();
  await expect(agent.approvalCard).toHaveCount(0);
});

test('history: a reopened conversation shows its messages, plan and steps', { tag: ['@agent', '@us1-command'] }, async ({
  api,
  agent,
  agentApi,
  trackPage,
  page,
}, testInfo) => {
  const command = `Find my agent track and mute the drums (${testInfo.project.name})`;
  const track = await freshTrack(api, `us5-history-${testInfo.project.name}`);
  await trackPage.goto(track.id);
  await agent.openNewConversation();
  await agent.sendCommand(command);
  await approveTaintedMute(agent);
  await agent.expectAnswer(finalText('us1-command'));
  const conversationId = await agentApi.latestConversationId();
  expect((await agentApi.conversation(conversationId)).messages.map((m) => m.role)).toEqual(['user', 'assistant']);

  await page.reload();
  await agent.open();
  await agent.conversation(conversationId).click();
  await expect(agent.conversation(conversationId)).toHaveAttribute('aria-current', 'true');
  await expect(agent.messages('user')).toHaveText([command]);
  await expect(agent.messages('assistant')).toContainText([finalText('us1-command')]);
  await agent.expectPlan(3, 'done');
  for (const tool of ['search_library', 'open_mixer', 'mixer_set_channel']) {
    await agent.expectStep(tool, 'succeeded');
  }
});

/**
 * The search results carry library titles, which are external text and taint the run (FR-013; owner decision at
 * T080: titles keep tainting), so the mute after the search asks first; the test approves.
 */
async function approveTaintedMute(agent: AgentPanel): Promise<void> {
  await agent.expectApproval('mixer_set_channel', 'change');
  expect(await agent.approvalReasons()).toContain('tainted');
  await agent.approve();
}

/** Id of the 009 preset "Jazz trio" (presets.v1.json), used literally by `us6-restyle.json`. */
const jazzTrioId = '3f9c2a1e-6b4d-4e8f-a0c2-1d3e5f7a9b01';

test('create: a restyle asks as heavy and queues a generation', { tag: ['@agent', '@us6-restyle', '@generative'] }, async ({
  api,
  agent,
  generationApi,
  generations,
  trackPage,
}) => {
  requireGeneratorMode('ready');
  await generationApi.requireState('ready');
  expect((await generationApi.preset('Jazz trio')).id).toBe(jazzTrioId);
  const track = await sourceTrack(api);
  const before = (await generationApi.list({ sourceTrackId: track.id })).map((g) => g.id);
  await trackPage.goto(track.id);
  await agent.openNewConversation();

  await agent.sendCommand('Restyle this track as a jazz trio from 2 to 14 seconds');
  await agent.expectApproval('restyle_track', 'heavy');
  expect(await agent.approvalReasons()).toContain('risk-heavy');
  await expect(agent.byId('agent-approval-estimate')).toHaveAttribute('data-device', 'gpu');
  await expect(agent.byId('agent-approval-arguments')).toContainText(jazzTrioId);
  expect((await generationApi.list({ sourceTrackId: track.id })).map((g) => g.id), 'nothing runs before Approve').toEqual(before);

  await agent.approve();
  await agent.expectAnswer(finalText('us6-restyle'));
  await agent.expectStep('restyle_track', 'succeeded');
  const generation = await generationApi.waitForNewest({ sourceTrackId: track.id }, before);
  expect(generation.kind).toBe('restyle');
  await expect(generations.row(generation.id)).toBeVisible({ timeout: 30_000 });
  await expect(generations.row(generation.id)).toHaveAttribute('data-kind', 'restyle');
  await generationApi.cancel(generation.id);
});

test('create: an artist imitation is refused and starts nothing', { tag: ['@agent', '@us6-imitation', '@generative'] }, async ({
  agent,
  generationApi,
  page,
}) => {
  requireGeneratorMode('ready');
  await generationApi.requireState('ready');
  const before = (await generationApi.list()).map((g) => g.id);
  await page.goto('/songs/new');
  await agent.openNewConversation();

  await agent.sendCommand('Make a song in the style of Metallica');
  await agent.expectAnswer(finalText('us6-imitation'));
  // ui-test-ids 1.10.0: a guardrail denial shows no card; the step is failed with the refusal.
  await agent.expectStep('generate_song', 'failed');
  await expect(await agent.stepError('generate_song')).toContainText(/artist|genre/i);
  await expect(agent.approvalCard).toHaveCount(0);
  expect((await generationApi.list()).map((g) => g.id)).toEqual(before);
});
