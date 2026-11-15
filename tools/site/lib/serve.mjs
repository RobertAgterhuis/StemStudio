/**
 * Tiny static server for site/dist under the GitHub Pages base path (/StemStudio/), so local
 * checks see the same relative URLs as the published site. Local review only.
 * @module
 */
import { createServer } from "node:http";
import { existsSync, readFileSync, statSync } from "node:fs";
import { extname, join, normalize, sep } from "node:path";

/** Base path of the published site. */
export const BASE_PATH = "/StemStudio/";

/** Ports the StemStudio stacks use; the review server never takes them. */
const RESERVED = new Set([8080, 8081, 9080, 9081, 9180]);

const TYPES = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".woff2": "font/woff2",
  ".pdf": "application/pdf",
  ".txt": "text/plain; charset=utf-8",
};

/**
 * Resolves a request path to a file below the root, or null.
 * @param {string} root dist folder
 * @param {string} url request URL
 * @returns {string | null} file path
 */
function resolveFile(root, url) {
  const path = decodeURIComponent(new URL(url, "http://localhost").pathname);
  if (!path.startsWith(BASE_PATH)) return null;
  const file = normalize(join(root, path.slice(BASE_PATH.length) || "index.html"));
  if (!file.startsWith(normalize(root) + sep) && file !== normalize(root)) return null;
  const target = existsSync(file) && statSync(file).isDirectory() ? join(file, "index.html") : file;
  return existsSync(target) ? target : null;
}

/**
 * Starts the server on a free port chosen by the OS (never a StemStudio port).
 * @param {string} root dist folder
 * @returns {Promise<{ url: string, close: () => Promise<void> }>} the site URL and a stop function
 */
export async function serve(root) {
  const server = createServer((req, res) => {
    const file = resolveFile(root, req.url ?? "/");
    if (!file) {
      res.writeHead(404, { "content-type": "text/plain" }).end("not found");
      return;
    }
    res.writeHead(200, { "content-type": TYPES[extname(file)] ?? "application/octet-stream" }).end(readFileSync(file));
  });
  for (;;) {
    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    const { port } = server.address();
    if (!RESERVED.has(port)) {
      return { url: `http://127.0.0.1:${port}${BASE_PATH}`, close: () => new Promise((resolve) => server.close(() => resolve())) };
    }
    await new Promise((resolve) => server.close(resolve));
  }
}
