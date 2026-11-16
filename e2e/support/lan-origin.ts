/**
 * A base URL of the Web app under a host name that is **not** a loopback name, for the claude-key
 * local-only check (CK-FR-012, ui-test-ids 1.21.0 `settings-agent-claude-key-local-only`).
 *
 * `E2E_LAN_BASE_URL` wins (for example `http://web:8080` when Playwright runs on the compose
 * network, quickstart Q4). Otherwise the first non-internal IPv4 address of this computer is used
 * with the port of `E2E_BASE_URL`: compose publishes 8080 on every interface, so the page is the
 * one a device on the LAN would see.
 *
 * @packageDocumentation
 */
import { networkInterfaces } from 'node:os';
import { webBaseUrl } from './env';

/** The loopback host names the Web app accepts for key entry (`PageOrigin.IsLoopbackHost`). */
export const loopbackHosts: readonly string[] = ['localhost', '127.0.0.1', '[::1]'];

/** True when `url`'s host is one of the loopback host names. */
export function isLoopbackUrl(url: string): boolean {
  return loopbackHosts.includes(new URL(url).hostname.toLowerCase());
}

/** The first non-internal IPv4 address of this computer, or undefined. */
function lanAddress(): string | undefined {
  const addresses = Object.values(networkInterfaces()).flatMap((entries) => entries ?? []);
  return addresses.find((entry) => entry.family === 'IPv4' && !entry.internal)?.address;
}

/** The non-loopback base URL, or undefined when neither the variable nor a LAN address exists. */
export function nonLoopbackBaseUrl(): string | undefined {
  const configured = process.env.E2E_LAN_BASE_URL ?? '';
  if (configured !== '') {
    return configured;
  }
  const address = lanAddress();
  if (address === undefined) {
    return undefined;
  }
  const url = new URL(webBaseUrl);
  url.hostname = address;
  return url.origin;
}
