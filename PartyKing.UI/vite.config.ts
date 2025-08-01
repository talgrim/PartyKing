/// <reference types="vitest" />
import { defineConfig, splitVendorChunkPlugin } from 'vite';
import react from '@vitejs/plugin-react';
import tsConfigPaths from 'vite-tsconfig-paths';

// https://vitejs.dev/config/
// eslint-disable-next-line import/no-default-export
export default defineConfig({
  server: { port: 2137 },
  plugins: [tsConfigPaths(), react(), splitVendorChunkPlugin()],
  build: {
    sourcemap: true,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    globalSetup: ['./testGlobalSetup.ts'],
    setupFiles: ['./testSetup.ts', '@vitest/web-worker'],
    deps: {
      inline: ['vitest-canvas-mock'],
    },
    exclude: [
      'node_modules',
      'e2e',
      'playwright-report',
      'html-report',
      'xray-report',
      '**/{vite,playwright}.config.*',
    ],
    coverage: {
      reporter: ['text', 'text-summary', 'json-summary', 'html'],
      reportsDirectory: './coverage',
      lines: 80,
      functions: 68,
      branches: 85,
      statements: 80,
      exclude: [
        'src/routes/**',
        'src/constants/**',
        'src/assets/**',
        'src/types/**',
        'test/**',
        'e2e/**',
        '*.test.*',
        '.env.*',
        '*Styled*.*',
      ],
    },
  },
});
