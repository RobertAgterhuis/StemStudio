import { defineConfig } from 'vite';
import { libraryBuild } from './vite.shared';

/**
 * Second entry (contract 2.3.0): the AudioWorklet processor as one self-contained ES module, loaded with
 * `audioWorklet.addModule('./js/time-stretch-processor.js')`. Built separately so no chunk is shared with
 * `audio-engine.js` (the worklet scope must not depend on a second, unpredictable file).
 */
export default defineConfig(libraryBuild('./src/stretch/processor.ts', 'time-stretch-processor.js'));
