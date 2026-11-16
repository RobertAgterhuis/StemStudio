import { expect, test } from '@playwright/test';
import { allSentinels, claudeKeyPrefix, findLeaks, lastFour, searchForms, sentinels } from '../support/claude-key';
import { isLoopbackUrl } from '../support/lan-origin';

// Stack-free self-test of the claude-key E2E helpers (011 follow-up claude-key, T047): the
// sentinels pass the API's format rule (CK-FR-006) and the leak search finds every form.

/** The format rule of `ClaudeApiKey.Parse` (CK-FR-006), without the admin rule. */
function wellFormed(key: string): boolean {
  return key.startsWith(claudeKeyPrefix) && key.length >= 40 && key.length <= 256 && /^[A-Za-z0-9_-]+$/.test(key);
}

test('every sentinel but the admin one is a well-formed, non-admin key; the admin one is admin-shaped', {
  tag: ['@self-test'],
}, () => {
  const { admin, ...usable } = sentinels;
  for (const key of Object.values(usable)) {
    expect(wellFormed(key), lastFour(key)).toBe(true);
    expect(key.toLowerCase().startsWith(`${claudeKeyPrefix}admin`)).toBe(false);
  }
  expect(wellFormed(admin)).toBe(true);
  expect(admin.startsWith(`${claudeKeyPrefix}admin`)).toBe(true);
  expect(new Set(allSentinels.map(lastFour)).size).toBe(allSentinels.length);
});

test('the leak search finds the plain, base64, UTF-16 base64 and raw UTF-16 forms and names no value', {
  tag: ['@self-test'],
}, () => {
  for (const form of searchForms(sentinels.stored)) {
    const reports = findLeaks('sample', Buffer.concat([Buffer.from('before '), form.bytes, Buffer.from(' after')]));
    expect(reports).toContain(`sample: sentinel stored as ${form.form}`);
    expect(reports.join(' ')).not.toContain(sentinels.stored);
  }
  expect(findLeaks('clean', `ends with ...${lastFour(sentinels.stored)}`)).toEqual([]);
});

test('only localhost, 127.0.0.1 and [::1] are loopback host names', { tag: ['@self-test'] }, () => {
  expect(['http://localhost:8080', 'http://127.0.0.1:8080', 'http://[::1]:8080'].map(isLoopbackUrl)).toEqual([true, true, true]);
  expect(['http://web:8080', 'http://192.168.1.20:8080', 'http://localhost.example:8080'].map(isLoopbackUrl)).toEqual([
    false,
    false,
    false,
  ]);
});
