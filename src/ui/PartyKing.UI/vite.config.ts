/// <reference types="vitest" />
import { defineConfig, Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tsConfigPaths from 'vite-tsconfig-paths';
import tailwindcss from '@tailwindcss/vite';


// The API and the Spotify login redirect use 127.0.0.1. A page opened via `localhost` is a different site for the
// browser, so the SameSite=Lax login cookies never reach the API. Send `localhost` visitors to 127.0.0.1.
const redirectLocalhostToLoopbackIp = (): Plugin => ({
  name: 'redirect-localhost-to-127.0.0.1',
  configureServer: (server) => {
    server.middlewares.use((req, res, next) => {
      const host = req.headers.host ?? '';
      if (!/^localhost(:\d+)?$/i.test(host)) {
        return next();
      }
      res.statusCode = 307;
      res.setHeader('Location', `http://${host.replace(/^localhost/i, '127.0.0.1')}${req.url ?? '/'}`);
      res.end();
    });
  },
});

// https://vitejs.dev/config/
// eslint-disable-next-line import/no-default-export
export default defineConfig({
  server: { 
    port: 2137,
    host: "0.0.0.0",
    watch: {
      usePolling: true // Fix for HMR between WSL2 and windows
    }
  },
  plugins: [redirectLocalhostToLoopbackIp(), tailwindcss(), tsConfigPaths(), react()],
  build: {
    sourcemap: true,
  },
  test: {
    globals: true,
    environment: 'jsdom',
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
      thresholds: {
        lines: 80,
        functions: 68,
        branches: 85,
        statements: 80,
      },
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
