import { defineConfig } from 'vite';
import { libraryBuild } from './vite.shared';

/**
 * Library build: one ES module at a fixed path inside the Blazor app's static assets, imported
 * from .NET with `JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/audio-engine.js")`.
 */
export default defineConfig(libraryBuild('./src/index.ts', 'audio-engine.js'));
