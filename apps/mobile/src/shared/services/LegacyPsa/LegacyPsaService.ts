import { getDefaultStore } from 'jotai';

import { LegacyPsaApi } from '@/shared/api/endpoints/LegacyPsa';
import { ConfigService } from '@/shared/services/Config';
import { EncryptionService } from '@/shared/services/Encryption';
import { legacyIvAtom } from '@/shared/state/atoms/legacyIvAtom';

const store = getDefaultStore();

const LEGACY_CASE_SERVICE = 'Case';

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

// Real shape: { isSuccess, message, data: [{ tokenId, iv }] } — `data` is an
// array, not an object (confirmed via a live capture of /Authentication/IV).
function extractIv(raw: unknown): string | undefined {
  const record = asRecord(raw);
  const rawData = record.data;
  const dataRecord = Array.isArray(rawData) ? asRecord(rawData[0]) : asRecord(rawData ?? record);
  const iv = dataRecord.iv ?? dataRecord.IV ?? record.iv ?? record.IV;
  return typeof iv === 'string' && iv.length > 0 ? iv : undefined;
}

function decryptIfNeeded<T>(raw: unknown, key: string, iv: string): T {
  if (typeof raw !== 'string') {
    return raw as T;
  }

  return EncryptionService.decrypt(raw, key, iv) as T;
}

async function requestToken(): Promise<string | undefined> {
  const raw = await LegacyPsaApi.generateToken();
  const iv = extractIv(raw);
  store.set(legacyIvAtom, iv ?? null);
  return iv;
}

// Concurrent callers (e.g. all 7 dashboard calls firing at once) share this
// single in-flight request instead of each triggering their own generateToken().
let refreshPromise: Promise<string | undefined> | null = null;

export const LegacyPsaService = {
  async refreshToken(): Promise<string | undefined> {
    if (!refreshPromise) {
      refreshPromise = requestToken().finally(() => {
        refreshPromise = null;
      });
    }

    return refreshPromise;
  },

  async getIv(): Promise<string> {
    const cached = store.get(legacyIvAtom);
    if (cached) {
      return cached;
    }

    const iv = await LegacyPsaService.refreshToken();
    if (!iv) {
      throw new Error('Unable to obtain the legacy encryption IV from Generate Token.');
    }

    return iv;
  },

  clearToken(): void {
    store.set(legacyIvAtom, null);
  },

  async callCaseService<TResponse = unknown>(
    method: string,
    params: Record<string, unknown> = {}
  ): Promise<TResponse> {
    const iv = await LegacyPsaService.getIv();
    const key = ConfigService.getLegacyApiKey();
    const payload = { Service: LEGACY_CASE_SERVICE, Method: method, ...params };
    // TEMP DEBUG: remove once the Service/Request request/response shapes are confirmed.
    console.log(`[LegacyPsaService] request payload for ${method}:`, payload);
    const encryptedValue = EncryptionService.encrypt(payload, key, iv);

    const raw = await LegacyPsaApi.serviceRequest({ iv, value: encryptedValue });
    const decrypted = decryptIfNeeded<TResponse>(raw, key, iv);
    // TEMP DEBUG: remove once the Service/Request response shape is confirmed.
    console.log(`[LegacyPsaService] decrypted response for ${method}:`, decrypted);
    return decrypted;
  },
};
