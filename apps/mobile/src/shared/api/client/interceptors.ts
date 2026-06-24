import type { AxiosError, AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';

import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import { ApiError } from '@/shared/types/api';

type UnauthorizedHandler = () => void | Promise<void>;

let unauthorizedHandler: UnauthorizedHandler | undefined;

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

function headerValue(headers: AxiosResponse['headers'] | undefined, name: string): string | undefined {
  const value = headers?.[name] ?? headers?.[name.toLowerCase()];
  return Array.isArray(value) ? value[0] : value?.toString();
}

function toApiError(error: AxiosError): ApiError {
  const data = error.response?.data as Partial<{
    code: string;
    message: string;
    details: unknown;
  }>;

  return new ApiError({
    code: data?.code ?? 'API_ERROR',
    message: data?.message ?? error.message ?? 'Unexpected API error',
    statusCode: error.response?.status,
    correlationId: headerValue(error.response?.headers, 'x-correlation-id'),
    details: data?.details ?? data,
  });
}

export function registerUnauthorizedHandler(handler: UnauthorizedHandler): void {
  unauthorizedHandler = handler;
}

export function attachInterceptors(apiClient: AxiosInstance): void {
  apiClient.interceptors.request.use(async (config) => {
    const token = await SecureStorageService.getItem(STORAGE_KEYS.ACCESS_TOKEN);
    if (token) {
      setHeader(config, 'Authorization', `Bearer ${token}`);
    }

    setHeader(config, 'X-Correlation-Id', generateUUID());
    return config;
  });

  apiClient.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      if (error.response?.status === 401) {
        await SecureStorageService.clearAll();
        await unauthorizedHandler?.();
      }

      return Promise.reject(toApiError(error));
    }
  );
}
