import { apiBaseUrl } from '../support/env';
import { expect, test } from '../support/test';

// Harness smoke test: the stack is up (tasks.md Appendix B, "Health").
test.describe('health', () => {
  test('web front end reports ok', async ({ request }) => {
    const response = await request.get('/health');
    expect(response.ok()).toBe(true);
    expect(await response.json()).toMatchObject({ status: 'ok' });
  });

  test('API reports ok', async ({ request }) => {
    const response = await request.get(`${apiBaseUrl}/health`);
    expect(response.ok()).toBe(true);
    expect(await response.json()).toMatchObject({ status: 'ok' });
  });
});
