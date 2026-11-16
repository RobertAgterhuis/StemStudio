import { fileURLToPath } from 'node:url';
import type { UserConfig } from 'vite';

/**
 * Library build of one entry into the Blazor app's static assets (`wwwroot/js`): one ES module, source map, no
 * minification. Shared by `vite.config.ts` (the engine) and `vite.processor.config.ts` (the AudioWorklet processor).
 *
 * @param entry - Entry module relative to this directory.
 * @param fileName - Output file name inside `wwwroot/js`.
 * @returns The Vite configuration.
 */
export function libraryBuild(entry: string, fileName: string): UserConfig {
  return {
    build: {
      lib: {
        entry: fileURLToPath(new URL(entry, import.meta.url)),
        formats: ['es'],
        fileName: () => fileName,
      },
      outDir: fileURLToPath(new URL('../StemStudio.Web/wwwroot/js', import.meta.url)),
      emptyOutDir: false,
      sourcemap: true,
      target: 'es2022',
      minify: false,
    },
  };
}
