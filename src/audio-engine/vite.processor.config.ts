import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

/**
 * Second entry (contract 2.3.0): the AudioWorklet processor as one self-contained ES module, loaded with
 * `audioWorklet.addModule('./js/time-stretch-processor.js')`. Built separately so no chunk is shared with
 * `audio-engine.js` (the worklet scope must not depend on a second, unpredictable file).
 */
export default defineConfig({
  build: {
    lib: {
      entry: fileURLToPath(new URL('./src/stretch/processor.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'time-stretch-processor.js',
    },
    outDir: fileURLToPath(new URL('../StemStudio.Web/wwwroot/js', import.meta.url)),
    emptyOutDir: false,
    sourcemap: true,
    target: 'es2022',
    minify: false,
  },
});
