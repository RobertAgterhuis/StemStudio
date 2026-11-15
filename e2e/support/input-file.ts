/**
 * Selecting files on a Blazor `InputFile` (features 005 and 007 reuse the 001 rule).
 *
 * @packageDocumentation
 */
import { expect, type Locator } from '@playwright/test';

/**
 * Sets `files` on a Blazor `InputFile` once it is wired. Blazor attaches its `change` listener
 * in a JS `init` call after the first render; a selection made before that is lost, so poll for
 * the marker `init` sets on the element.
 */
export async function setBlazorInputFiles(input: Locator, files: string | readonly string[]): Promise<void> {
  await expect.poll(() => input.evaluate((el) => '_blazorInputFileNextFileId' in el)).toBe(true);
  await input.setInputFiles(typeof files === 'string' ? files : [...files]);
}
