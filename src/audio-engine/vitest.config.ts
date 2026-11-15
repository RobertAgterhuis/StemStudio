import { defineConfig } from 'vitest/config';

/** Unit tests run in jsdom with a mocked AudioContext (no real audio device). */
export default defineConfig({
  test: {
    environment: 'jsdom',
    include: ['tests/**/*.test.ts'],
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      include: ['src/**/*.ts'],
      exclude: ['src/types.ts'],
      reporter: ['text', 'cobertura', 'lcov'],
      reportsDirectory: 'coverage',
      thresholds: {
        lines: 90,
        branches: 90,
        functions: 90,
        statements: 90,
      },
    },
  },
});
