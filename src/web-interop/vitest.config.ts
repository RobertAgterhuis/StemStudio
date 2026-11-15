import { defineConfig } from 'vitest/config';

/** Coverage gate (constitution I): every metric at least this percentage. */
const gate = 90;

/** Unit tests run in jsdom; local storage, matchMedia and driver.js are faked (no real browser). */
export default defineConfig({
  test: {
    include: ['tests/**/*.test.ts'],
    environment: 'jsdom',
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'cobertura', 'lcov'],
      reportsDirectory: 'coverage',
      include: ['src/**/*.ts'],
      exclude: ['src/types.ts'],
      thresholds: { lines: gate, branches: gate, functions: gate, statements: gate },
    },
  },
});
