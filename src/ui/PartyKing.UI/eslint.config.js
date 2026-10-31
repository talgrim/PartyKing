import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import importPlugin from 'eslint-plugin-import';
import unusedImports from 'eslint-plugin-unused-imports';
import prettier from 'eslint-config-prettier';

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'node_modules', 'public'] },

  js.configs.recommended,
  ...tseslint.configs.recommended,
  reactHooks.configs['recommended-latest'],
  reactRefresh.configs.vite,

  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 'latest',
      globals: { ...globals.browser },
    },
    plugins: {
      import: importPlugin,
      'unused-imports': unusedImports,
    },
    settings: {
      // Resolves the "@/" alias (and the other paths) from tsconfig.json
      'import/resolver': { typescript: { project: './tsconfig.json' } },
      'import/parsers': { '@typescript-eslint/parser': ['.ts', '.tsx'] },
    },
    rules: {
      // Unused imports are removed by --fix; unused variables keep the "_" escape hatch
      '@typescript-eslint/no-unused-vars': 'off',
      'unused-imports/no-unused-imports': 'error',
      'unused-imports/no-unused-vars': [
        'warn',
        { vars: 'all', varsIgnorePattern: '^_', args: 'after-used', argsIgnorePattern: '^_' },
      ],

      'import/no-unresolved': 'error',
      'import/no-duplicates': 'error',
      'import/first': 'error',
    },
  },

  // Tests and their setup run on Vitest's globals (describe, it, expect, vi) in jsdom
  {
    files: ['**/*.test.{ts,tsx}', 'testSetup.ts'],
    languageOptions: { globals: { ...globals.browser, ...globals.vitest } },
  },

  // Config and build files run on Node
  {
    files: ['*.{js,mjs,ts}'],
    languageOptions: { globals: { ...globals.node } },
  },

  // The code style is not enforced by ESLint, so formatting rules stay out of the way
  prettier,
);
