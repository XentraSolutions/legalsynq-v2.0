import type { AxiosError, AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';
import { getDefaultStore } from 'jotai';

import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { ConfigService } from '@/shared/services/Config';
import { ErrorTrackingService } from '@/shared/services/ErrorTracking';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';
import { authAtom } from '@/shared/state/atoms/authAtom';
import { ApiError } from '@/shared/types/api';

type UnauthorizedHandler = () => void | Promise<void>;

let unauthorizedHandler: UnauthorizedHandler | undefined;

const store = getDefaultStore();

function setHeader(config: InternalAxiosRequestConfig, key: string, value: string): void {
  const headers = config.headers as unknown as Record<string, string>;
  headers[key] = value;
}

function generateUUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (character) => {
    const random = (Math.random() * 16) | 0;
    const value = character === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}

function headerValue(
  headers: AxiosResponse['headers'] | undefined,
  name: string
): string | undefined {
  const value = headers?.[name] ?? headers?.[name.toLowerCase()];
  return Array.isArray(value) ? value[0] : value?.toString();
}

function toApiError(error: AxiosError): ApiError {
  const data = error.response?.data as Partial<{
    code: string;
    errorCode: string;
    detail: string;
    message: string;
    title: string;
    details: unknown;
  }>;

  return new ApiError({
    code: data?.errorCode ?? data?.code ?? data?.title ?? 'API_ERROR',
    message:
      data?.message ?? data?.detail ?? data?.title ?? error.message ?? 'Unexpected API error',
    statusCode: error.response?.status,
    correlationId: headerValue(error.response?.headers, 'x-correlation-id'),
    details: data?.details ?? data,
  });
}

function redactSensitiveFields(data: unknown): unknown {
  if (Array.isArray(data)) return data.map(redactSensitiveFields);
  if (!data || typeof data !== 'object') return data;

  return Object.fromEntries(
    Object.entries(data).map(([key, value]) => [
      key,
      /password|token|secret/i.test(key) ? '[redacted]' : redactSensitiveFields(value),
    ])
  );
}

function parseRequestData(data: unknown): unknown {
  if (!data) {
    return undefined;
  }

  if (typeof FormData !== 'undefined' && data instanceof FormData) {
    return '[form-data]';
  }

  if (typeof data === 'string') {
    try {
      return redactSensitiveFields(JSON.parse(data) as unknown);
    } catch {
      return '[string request body omitted]';
    }
  }

  return redactSensitiveFields(data);
}

function captureApiError(error: AxiosError, apiError: ApiError): void {
  ErrorTrackingService.captureApiError(apiError, {
    baseURL: error.config?.baseURL,
    code: apiError.code,
    correlationId: apiError.correlationId,
    method: error.config?.method?.toUpperCase(),
    params: error.config?.params,
    requestBody: parseRequestData(error.config?.data),
    responseData: error.response?.data,
    statusCode: error.response?.status,
    timeout: error.config?.timeout,
    url: error.config?.url,
  });
}

export function registerUnauthorizedHandler(handler: UnauthorizedHandler): void {
  unauthorizedHandler = handler;
}

export function attachInterceptors(apiClient: AxiosInstance): void {
  apiClient.interceptors.request.use(async (config) => {
    const mode = store.get(apiModeAtom);

    if (mode === 'legacy') {
      config.baseURL = ConfigService.getLegacyApiBaseUrl();
      setHeader(config, 'apiKey', ConfigService.getLegacyApiKey());
    }

    const token =
      mode === 'legacy'
        ? await SecureStorageService.getItem(STORAGE_KEYS.LEGACY_ACCESS_TOKEN)
        : store.get(authAtom).token;
    if (token) {
      if (mode === 'legacy') {
        // The legacy backend authenticates via a `sessionId` header, not a bearer token.
        setHeader(config, 'sessionId', token);
      } else {
        setHeader(config, 'Authorization', `Bearer ${token}`);
      }
    }

    setHeader(config, 'X-Correlation-Id', generateUUID());
    return config;
  });

  apiClient.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      if (error.response?.status === 401) {
        await unauthorizedHandler?.();
      }

      const apiError = toApiError(error);
      captureApiError(error, apiError);
      return Promise.reject(apiError);
    }
  );
}
