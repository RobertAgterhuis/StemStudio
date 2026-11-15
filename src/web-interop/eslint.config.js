import js from '@eslint/js';
import jsdoc from 'eslint-plugin-jsdoc';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/** Flat ESLint config: type-checked typescript-eslint rules plus SIG-aligned size limits. */
export default tseslint.config(
  { ignores: ['coverage/**', 'node_modules/**', 'dist/**'] },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  ...tseslint.configs.stylisticTypeChecked,
  {
    files: ['src/**/*.ts'],
    plugins: { jsdoc },
    rules: {
      'jsdoc/require-jsdoc': [
        'error',
        {
          publicOnly: true,
          require: { FunctionDeclaration: true, ArrowFunctionExpression: true, ClassDeclaration: true },
          contexts: ['TSInterfaceDeclaration', 'TSTypeAliasDeclaration', 'TSMethodSignature'],
        },
      ],
      'jsdoc/check-alignment': 'error',
      'jsdoc/no-types': 'error',
    },
  },
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
