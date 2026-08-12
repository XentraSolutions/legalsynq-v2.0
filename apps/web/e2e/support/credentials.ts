import fs from 'node:fs';
import path from 'node:path';
import type { E2EEnvName } from '../config/environments';

/**
 * Loads live tenant credentials for real e2e tests from e2e/data/credentials.json.
 *
 * That file is gitignored (managed outside the repo) — see
 * e2e/data/credentials.example.json for the expected shape:
 *
 *   { "<platform>": { "default": {...}, "<envName>"?: {...} } }
 *
 * Each platform has a "default" credential set used by every environment
 * that shares the same backend/tenant data (currently "local" and "qa" both
 * point at the QA backend). An environment-specific key (e.g. "production")
 * overrides the default when that environment uses a different account.
 */

export interface PlatformCredentials {
  tenantCode: string;
  username: string;
  password: string;
}

type CredentialsFile = Record<string, Record<string, PlatformCredentials>>;

const CREDENTIALS_PATH = path.join(__dirname, '..', 'data', 'credentials.json');

let cache: CredentialsFile | null = null;

function loadAll(): CredentialsFile {
  if (cache) return cache;

  if (!fs.existsSync(CREDENTIALS_PATH)) {
    throw new Error(
      `Missing ${CREDENTIALS_PATH}. Real e2e tests need live tenant credentials — ` +
      `copy e2e/data/credentials.example.json to e2e/data/credentials.json and fill in ` +
      `real values (that file is gitignored and is not committed).`,
    );
  }

  cache = JSON.parse(fs.readFileSync(CREDENTIALS_PATH, 'utf8'));
  return cache!;
}

export function getCredentials(platform: string, env: E2EEnvName): PlatformCredentials {
  const platformCreds = loadAll()[platform];
  if (!platformCreds) {
    throw new Error(`No credentials for platform "${platform}" in ${CREDENTIALS_PATH}.`);
  }

  const creds = platformCreds[env] ?? platformCreds.default;
  if (!creds) {
    throw new Error(
      `No credentials for platform "${platform}" env "${env}" (and no "default" fallback) ` +
      `in ${CREDENTIALS_PATH}.`,
    );
  }
  return creds;
}
