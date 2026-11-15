import js from '@eslint/js';
import jsdoc from 'eslint-plugin-jsdoc';
import playwright from 'eslint-plugin-playwright';
import tseslint from 'typescript-eslint';

/** Flat ESLint config: type-checked typescript-eslint, Playwright rules, SIG-aligned limits. */
export default tseslint.config(
  {
    ignores: [
      'node_modules/**',
      'test-results/**',
      'playwright-report/**',
      'blob-report/**',
      '.lighthouseci/**',
      'lighthouse-report/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: { allowDefaultProject: ['eslint.config.mjs'] },
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
    files: ['pages/**/*.ts', 'support/**/*.ts'],
    plugins: { jsdoc },
    rules: {
      'jsdoc/require-jsdoc': [
        'error',
        {
          publicOnly: true,
          require: { FunctionDeclaration: true, ClassDeclaration: true, MethodDefinition: true },
          contexts: ['TSInterfaceDeclaration', 'TSTypeAliasDeclaration'],
        },
      ],
      'jsdoc/check-alignment': 'error',
      'jsdoc/no-types': 'error',
    },
  },
  {
    files: ['tests/**/*.ts', 'visual/**/*.ts', 'a11y/**/*.ts', 'lighthouse/**/*.setup.ts'],
    ...playwright.configs['flat/recommended'],
    rules: {
      ...playwright.configs['flat/recommended'].rules,
      'playwright/no-wait-for-timeout': 'error',
      'playwright/no-force-option': 'error',
      'playwright/expect-expect': ['error', { assertFunctionNames: ['expect*', 'assert*'] }],
      'max-lines-per-function': ['error', { max: 60, skipBlankLines: true, skipComments: true }],
    },
  },
  {
    files: ['**/*.mjs'],
    extends: [tseslint.configs.disableTypeChecked],
  },
);
