import { defineConfig } from 'vite';
import { libraryBuild } from './vite.shared';

/**
 * Third entry (contract 2.5.0): the capture AudioWorklet processor as one self-contained ES module, loaded with
 * `audioWorklet.addModule('./js/audio-engine-capture-worklet.js')`. Built separately so the worklet scope never
 * depends on a shared chunk.
 */
export default defineConfig(libraryBuild('./src/recording/capture-worklet.ts', 'audio-engine-capture-worklet.js'));
