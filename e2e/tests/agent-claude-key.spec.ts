import type { ClaudeKeySection } from '../pages/claude-key-section';
import { axeViolationsIn } from '../support/axe';
import { claudeKeyVariable, findLeaks, lastFour, sentinels } from '../support/claude-key';
import {
  contextAt,
  expectLocalOnlyRefusal,
  expectNoEnvironmentKey,
  keyStatusFromApi,
  removeStoredKey,
  requireLanBaseUrl,
  requireLoopbackBaseUrl,
} from '../support/claude-key-flow';
import {
  apiRestartTimeoutMs,
  containerLogs,
  databaseFiles,
  loseKeyRing,
  startApiWithEnvironmentKey,
} from '../support/claude-key-stack';
import { expect, test } from '../support/agent-test';
import { LeakWatch } from '../support/leak-watch';

// 011 follow-up claude-key (T046, T047; TEST-STRATEGY 17.10): the Anthropic key entered in
// Settings and stored encrypted. Opt-in `@claude-key` (`npm run test:claude-key`), excluded from
// `npm test`: the specs change the stored key, recreate or restart the api and read container
// logs, so they need the Docker CLI on the host and a stack started WITHOUT ANTHROPIC_API_KEY.
// Every key write goes through the UI (the API refuses writes that do not come from the Web app).
// Sentinels are built at run time (support/claude-key.ts) and are never sent to Anthropic:
// no test presses "Test key" (the free model lookup would send the key to the real service).

const keySection = '[data-testid="settings-agent-claude-key"]';

/** Start of this spec file's run: the container log scan reads from here. */
const suiteStart = new Date().toISOString();

/** Opens Settings and removes a key left over by an earlier test, so every test starts at "No key". */
async function openWithoutStoredKey(claudeKey: ClaudeKeySection): Promise<void> {
  await claudeKey.openAndDeleteIfStored();
  await claudeKey.expectStatus({ state: 'not-configured', source: '', stored: 'absent' });
}

test.beforeAll(async () => {
  await expectNoEnvironmentKey();
});

test.beforeEach(() => {
  requireLoopbackBaseUrl();
});

test.afterAll(async ({ browser }) => {
  await removeStoredKey(browser);
});

test('not configured: empty password field, Save disabled, the 011 variable as alternative, no axe violation', {
  tag: ['@claude-key'],
}, async ({ claudeKey, settings, page }) => {
  await openWithoutStoredKey(claudeKey);

  await expect(claudeKey.input).toHaveAttribute('type', 'password');
  await expect(claudeKey.input).toHaveAttribute('autocomplete', 'off');
  await expect(claudeKey.input).toHaveAttribute('spellcheck', 'false');
  await expect(claudeKey.input).toHaveAttribute('maxlength', '512');
  await expect(claudeKey.input).toHaveValue('');
  await expect(claudeKey.save).toBeDisabled();
  await expect(claudeKey.status).toContainText(claudeKeyVariable);
  await expect(settings.claudeStatus).toHaveAttribute('data-key-configured', 'false');
  await expect(claudeKey.byId('settings-agent-claude-key-test')).toHaveCount(0);

  expect(await axeViolationsIn(page, keySection)).toEqual([]);
});

test('malformed: an admin key is refused with a readable error that never repeats the value', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi }) => {
  await openWithoutStoredKey(claudeKey);

  await claudeKey.saveKey(sentinels.admin);

  await expect(claudeKey.error).toBeVisible();
  await expect(claudeKey.error).toContainText(/admin key/i);
  expect(findLeaks('error text', await claudeKey.error.innerText())).toEqual([]);
  await claudeKey.expectDomWithout();
  await claudeKey.expectStatus({ state: 'not-configured', source: '', stored: 'absent' });
  expect((await keyStatusFromApi(agentApi)).stored, 'nothing is stored').toBe('absent');
});

test('store: saved encrypted, status with the last four, backend unchanged, never in DOM, network or console', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi, settings, page }) => {
  const watch = new LeakWatch(page, Object.values(sentinels));
  await openWithoutStoredKey(claudeKey);
  const backendBefore = (await agentApi.settings()).backend;

  await claudeKey.saveKey(sentinels.stored);

  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, lastFour(sentinels.stored));
  await expect(claudeKey.status).toContainText('stored encrypted');
  await expect(settings.claudeStatus).toHaveAttribute('data-key-configured', 'true');
  await claudeKey.expectDomWithout();
  const key = await keyStatusFromApi(agentApi);
  expect(key).toMatchObject({ configured: true, state: 'stored', lastFour: 'STO1', storedLastFour: 'STO1' });
  expect((await agentApi.settings()).backend, 'saving a key never changes the backend (CK-FR-015)').toBe(backendBefore);

  await page.reload();
  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, 'STO1');
  await expect(claudeKey.input, 'the field is hidden while a key is stored').toHaveCount(0);
  await claudeKey.expectDomWithout();
  expect(await axeViolationsIn(page, keySection)).toEqual([]);
  await watch.expectClean();
});

test('replace: Replace key stores the new key; Cancel closes the field without a change', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi, page }) => {
  const watch = new LeakWatch(page, Object.values(sentinels));
  await openWithoutStoredKey(claudeKey);
  await claudeKey.saveKey(sentinels.stored);

  await claudeKey.byId('settings-agent-claude-key-replace').click();
  await expect(claudeKey.input, 'never pre-filled').toHaveValue('');
  await claudeKey.byId('settings-agent-claude-key-cancel').click();
  await expect(claudeKey.input).toHaveCount(0);
  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, 'STO1');

  await claudeKey.replaceKey(sentinels.replacement);

  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, lastFour(sentinels.replacement));
  expect(await keyStatusFromApi(agentApi)).toMatchObject({ state: 'stored', lastFour: 'STO2', storedLastFour: 'STO2' });
  await claudeKey.expectDomWithout();
  await watch.expectClean();
});

test('delete: the confirmation keeps the key on Keep key and Escape, Delete key removes it', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi, page }) => {
  await openWithoutStoredKey(claudeKey);
  await claudeKey.saveKey(sentinels.stored);

  await claudeKey.openDeleteConfirmation();
  expect(await axeViolationsIn(page, keySection), 'confirmation open').toEqual([]);
  await claudeKey.byId('settings-agent-claude-key-delete-confirm-no').click();
  await expect(claudeKey.confirmation).toHaveCount(0);
  await claudeKey.openDeleteConfirmation();
  await page.keyboard.press('Escape');
  await expect(claudeKey.confirmation).toHaveCount(0);
  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, 'STO1');

  await claudeKey.deleteKey();

  await claudeKey.expectStatus({ state: 'not-configured', source: '', stored: 'absent' });
  await expect(claudeKey.input).toHaveValue('');
  expect(await keyStatusFromApi(agentApi)).toMatchObject({ configured: false, stored: 'absent', storedLastFour: null });
});

test('local-only: a key write that does not come from the Web app is refused with 403 local-only', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi }) => {
  await openWithoutStoredKey(claudeKey);
  await claudeKey.saveKey(sentinels.stored);

  await expectLocalOnlyRefusal(await agentApi.putClaudeKeyFromElsewhere(sentinels.replacement), 'PUT from elsewhere');
  await expectLocalOnlyRefusal(await agentApi.deleteClaudeKeyFromElsewhere(), 'DELETE from elsewhere');

  expect(await keyStatusFromApi(agentApi), 'neither refused call changed the key').toMatchObject({ state: 'stored', lastFour: 'STO1' });
});

test('local-only: under a non-loopback host name the section shows the notice and every key control is disabled', {
  tag: ['@claude-key'],
}, async ({ browser, claudeKey }) => {
  const lanUrl = requireLanBaseUrl();
  await openWithoutStoredKey(claudeKey);
  const context = await contextAt(browser, lanUrl);
  try {
    const lanPage = await context.newPage();
    await lanPage.goto('/settings');
    const notice = lanPage.getByTestId('settings-agent-claude-key-local-only');
    await expect(notice).toContainText('can only be entered on the computer StemStudio runs on');
    await expect(lanPage.getByTestId('settings-agent-claude-key-input')).toBeDisabled();
    await expect(lanPage.getByTestId('settings-agent-claude-key-save')).toBeDisabled();
  } finally {
    await context.close();
  }
});

test('environment wins: the variable is active, the stored key is shown as not in use', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi, page }) => {
  test.setTimeout(3 * apiRestartTimeoutMs);
  await openWithoutStoredKey(claudeKey);
  await claudeKey.saveKey(sentinels.stored);
  try {
    await startApiWithEnvironmentKey(sentinels.environment);
    await page.reload();

    await claudeKey.expectStatus({ state: 'environment', source: 'environment', stored: 'readable' }, lastFour(sentinels.environment));
    await expect(claudeKey.byId('settings-agent-claude-key-env-override')).toContainText('...STO1');
    await expect(claudeKey.byId('settings-agent-claude-key-test-source')).toBeVisible();
    await expect(claudeKey.byId('settings-agent-claude-key-test')).toBeEnabled();
    expect(await keyStatusFromApi(agentApi)).toMatchObject({ source: 'environment', lastFour: 'ENV1', storedLastFour: 'STO1' });
    await claudeKey.expectDomWithout();
    expect(await axeViolationsIn(page, keySection)).toEqual([]);
  } finally {
    await startApiWithEnvironmentKey(undefined);
  }
  await page.reload();
  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, 'STO1');
});

test('unreadable: after the key ring is lost the status says so and saving a new key recovers', {
  tag: ['@claude-key'],
}, async ({ claudeKey, agentApi, page }) => {
  test.setTimeout(3 * apiRestartTimeoutMs);
  await openWithoutStoredKey(claudeKey);
  await claudeKey.saveKey(sentinels.stored);

  await loseKeyRing();

  expect(await keyStatusFromApi(agentApi), 'one status call after the start (CK-SC-004)').toMatchObject({
    configured: false,
    state: 'stored-unreadable',
    stored: 'unreadable',
    storedLastFour: 'STO1',
  });
  expect(await agentApi.statusOf('/api/tracks'), 'the rest of the API works').toBe(200);
  await page.reload();
  await claudeKey.expectStatus({ state: 'stored-unreadable', source: '', stored: 'unreadable' });
  await expect(claudeKey.byId('settings-agent-claude-key-unreadable')).toContainText('Enter the key again');
  expect(await axeViolationsIn(page, keySection)).toEqual([]);

  await claudeKey.replaceKey(sentinels.recovered);

  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, lastFour(sentinels.recovered));
  await claudeKey.expectDomWithout();
});

test('no sentinel in the api and web container logs or in the SQLite files', {
  tag: ['@claude-key'],
}, async ({ claudeKey }) => {
  await openWithoutStoredKey(claudeKey);
  await claudeKey.saveKey(sentinels.stored);
  await claudeKey.expectStatus({ state: 'stored', source: 'stored', stored: 'readable' }, 'STO1');

  expect(findLeaks('api/web logs', containerLogs(suiteStart)), 'container logs (CK-SC-001)').toEqual([]);
  expect(findLeaks('stemstudio.db + -wal/-shm', databaseFiles()), 'database holds only the ciphertext').toEqual([]);

  await claudeKey.deleteKey();
});
