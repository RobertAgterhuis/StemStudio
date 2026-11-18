import { defineConfig } from 'vite';
import { libraryBuild } from './vite.shared';

/**
 * Fourth entry (contract 2.6.0, feature 016): the pitch-tracker AudioWorklet processor as one self-contained ES module,
 * loaded with `audioWorklet.addModule('./js/audio-engine-pitch-worklet.js')`. Built separately so the worklet scope never
 * depends on a shared chunk.
 */
export default defineConfig(libraryBuild('./src/pitch/worklet.ts', 'audio-engine-pitch-worklet.js'));
