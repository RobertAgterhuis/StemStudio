/**
 * Records what a page receives and logs, so a claude-key spec can prove that no sentinel key ever
 * comes back to the browser (CK-SC-001): HTTP response bodies, WebSocket frames **received** (the
 * Blazor circuit's render batches) and console messages.
 *
 * Frames the browser **sends** are not scanned: typing the key into the password field sends it
 * over the circuit to the Web app, which is the intended path of the value (browser -> Web -> API).
 *
 * @packageDocumentation
 */
import { expect, type Page, type Response, type WebSocket } from '@playwright/test';
import { findLeaks, type LeakReport } from './claude-key';

/** Collects the incoming traffic and console output of one page. */
export class LeakWatch {
  private readonly pending: Promise<LeakReport[]>[] = [];
  private readonly found: LeakReport[] = [];
  private counts = { responses: 0, frames: 0, console: 0 };

  /**
   * Starts recording; attach before the first navigation.
   * @param keys The sentinels to search for.
   */
  constructor(page: Page, private readonly keys: readonly string[]) {
    page.on('response', (response) => this.pending.push(this.scanResponse(response)));
    page.on('websocket', (socket) => {
      this.watchSocket(socket);
    });
    page.on('console', (message) => {
      this.counts.console++;
      this.found.push(...findLeaks(`console ${message.type()}`, message.text(), this.keys));
    });
  }

  /** How much was scanned (so a test can assert the watch saw the circuit at all). */
  get scanned(): Readonly<{ responses: number; frames: number; console: number }> {
    return { ...this.counts };
  }

  /** Waits for the pending response scans and returns every hit so far. */
  async leaks(): Promise<LeakReport[]> {
    const fromResponses = (await Promise.all(this.pending)).flat();
    return [...this.found, ...fromResponses];
  }

  /** Asserts that nothing received or logged contained a sentinel and that the circuit was observed. */
  async expectClean(): Promise<void> {
    expect(await this.leaks(), 'sentinel found in network or console').toEqual([]);
    expect(this.scanned.frames, 'no Blazor circuit frame was observed').toBeGreaterThan(0);
  }

  private watchSocket(socket: WebSocket): void {
    socket.on('framereceived', ({ payload }) => {
      this.counts.frames++;
      this.found.push(...findLeaks(`websocket ${socket.url()}`, payload, this.keys));
    });
  }

  private async scanResponse(response: Response): Promise<LeakReport[]> {
    this.counts.responses++;
    const body = await response.body().catch(() => Buffer.alloc(0));
    return findLeaks(`response ${response.url()}`, body, this.keys);
  }
}
