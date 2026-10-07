import js from '@eslint/js'
import svelte from 'eslint-plugin-svelte'
import globals from 'globals'
import ts from 'typescript-eslint'
import svelteConfig from './svelte.config.js'

export default ts.config(
  { ignores: ['dist/', 'node_modules/', 'coverage/'] },
  js.configs.recommended,
  ...ts.configs.strict,
  ...svelte.configs.recommended,
  {
    // __TONTUYAU_VERSION__: set by Vite's `define` (vite.config.ts, src/globals.d.ts).
    languageOptions: { globals: { ...globals.browser, ...globals.node, __TONTUYAU_VERSION__: 'readonly' } },
  },
  {
    files: ['**/*.svelte', '**/*.svelte.ts'],
    languageOptions: {
      parserOptions: { projectService: true, extraFileExtensions: ['.svelte'], parser: ts.parser, svelteConfig },
    },
  },
  {
    files: ['**/*.test.ts'],
    languageOptions: { globals: { ...globals.vitest } },
  },
)
