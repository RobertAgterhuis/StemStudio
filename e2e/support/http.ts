/**
 * Response helper shared by the feature 005 and 007 API clients.
 *
 * @packageDocumentation
 */
import { expect, type APIResponse } from '@playwright/test';

/** Asserts a 2xx response (the message names URL and status) and returns its JSON body. */
export async function okJson<T>(response: APIResponse): Promise<T> {
  expect(response.ok(), `${response.url()} -> ${String(response.status())}`).toBe(true);
  return (await response.json()) as T;
}
