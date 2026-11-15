import { fileURLToPath } from 'node:url';
import { defineConfig, type UserConfig } from 'vite';

const outDir = fileURLToPath(new URL('../StemStudio.Web/wwwroot/js', import.meta.url));

/** ES module imported from .NET: `JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/web-interop.js")`. */
const moduleBuild: UserConfig = {
  build: {
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'web-interop.js',
    },
    outDir,
    emptyOutDir: false,
    sourcemap: true,
    target: 'es2022',
    minify: false,
  },
};

/** Classic synchronous script, first element in `<head>` (web-interop.md 1): one IIFE, no imports, minified. */
const firstPaintBuild: UserConfig = {
  build: {
    lib: {
      entry: fileURLToPath(new URL('./src/first-paint.ts', import.meta.url)),
      name: 'StemStudioFirstPaint',
      formats: ['iife'],
      fileName: () => 'first-paint.js',
    },
    outDir,
    emptyOutDir: false,
    sourcemap: true,
    target: 'es2022',
    minify: true,
  },
};

/** `vite build` writes web-interop.js; `vite build --mode first-paint` writes first-paint.js. */
export default defineConfig(({ mode }) => (mode === 'first-paint' ? firstPaintBuild : moduleBuild));
