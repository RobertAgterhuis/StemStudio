import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

/**
 * Library build: one ES module at a fixed path inside the Blazor app's static assets, imported
 * from .NET with `JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/audio-engine.js")`.
 */
export default defineConfig({
  build: {
    lib: {
      entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'audio-engine.js',
    },
    outDir: fileURLToPath(new URL('../StemStudio.Web/wwwroot/js', import.meta.url)),
    emptyOutDir: false,
    sourcemap: true,
    target: 'es2022',
    minify: false,
  },
});
