import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/** Flat ESLint config: type-checked typescript-eslint rules plus SIG-aligned size limits. */
export default tseslint.config(
  { ignores: ['coverage/**', 'node_modules/**', 'dist/**'] },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      globals: { ...globals.browser },
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      complexity: ['error', 5],
      'max-params': ['error', 4],
      'max-lines-per-function': ['error', { max: 30, skipBlankLines: true, skipComments: true }],
      'max-depth': ['error', 3],
      '@typescript-eslint/explicit-module-boundary-types': 'error',
    },
  },
  {
    files: ['tests/**/*.ts'],
    rules: {
      'max-lines-per-function': 'off',
    },
  },
  {
    files: ['**/*.js'],
    extends: [tseslint.configs.disableTypeChecked],
  },
);
