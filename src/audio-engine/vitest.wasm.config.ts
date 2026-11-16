import { defineConfig } from 'vitest/config';

/**
 * Node suite against the real `soundtouch.wasm` (`npm run test:wasm`, contract 2.3.0 "Testing"). It runs only when the
 * module was built (`scripts/build-soundtouch.*`); otherwise it is skipped with a message, or fails when
 * `STEMSTUDIO_REQUIRE_WASM=1` (CI).
 */
export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests-wasm/**/*.test.ts'],
    testTimeout: 120_000,
  },
});
