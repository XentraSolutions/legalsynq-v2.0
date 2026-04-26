import { defineConfig, devices } from '@playwright/test';
import { execSync } from 'child_process';

function systemChromiumPath(): string | undefined {
  try {
    return execSync('which chromium 2>/dev/null || which google-chrome 2>/dev/null', {
      encoding: 'utf8',
    }).trim() || undefined;
  } catch {
    return undefined;
  }
}

const chromiumExe = systemChromiumPath();

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  retries: process.env.CI ? 1 : 0,

  use: {
    baseURL: 'http://localhost:3001',
    trace: 'on-first-retry',
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          executablePath: chromiumExe,
          args: ['--no-sandbox', '--disable-dev-shm-usage'],
        },
      },
    },
  ],

  webServer: [
    {
      name:                'mock-identity-api',
      command:             'node e2e/mock-identity-server.mjs',
      url:                 'http://localhost:15001',
      reuseExistingServer: !process.env.CI,
      timeout:             10_000,
    },
    {
      name:                'next-app',
      command:             'GATEWAY_URL=http://localhost:15001 npx next dev -p 3001',
      url:                 'http://localhost:3001',
      reuseExistingServer: !process.env.CI,
      timeout:             60_000,
    },
  ],
});
