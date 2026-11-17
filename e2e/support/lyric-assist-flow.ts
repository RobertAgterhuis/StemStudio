/**
 * Flows of the feature 023 specs: run an assist from the pane and wait on the id of the assist it
 * started (LESSONS qa: never only on a status the previous run may have left).
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import type { AssistButton, LyricAssistPane } from '../pages/lyric-assist-pane';
import type { LyricAssistApi, LyricAssistDto } from './lyric-assist-api';

/** Id of the newest assist of a sheet, or `''`. */
async function newestAssistId(api: LyricAssistApi, lyricsId: string): Promise<string> {
  return (await api.assists(lyricsId))[0]?.assistId ?? '';
}

/** Presses `button`, waits for the new assist to end and for the pane to show it as done. */
export async function runAssist(pane: LyricAssistPane, api: LyricAssistApi, lyricsId: string, button: AssistButton): Promise<LyricAssistDto> {
  const before = await newestAssistId(api, lyricsId);
  await pane.press(button);
  await expect.poll(() => newestAssistId(api, lyricsId), { message: `${button} started a new assist`, timeout: 30_000 }).not.toBe(before);
  const assist = await api.waitForEnd(await newestAssistId(api, lyricsId));
  expect(assist.status, `${button} assist`).toBe('completed');
  await expect(pane.pane).toHaveAttribute('data-state', 'done');
  return assist;
}
