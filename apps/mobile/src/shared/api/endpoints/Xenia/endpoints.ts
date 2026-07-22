import EventSource, { type CustomEvent, type ErrorEvent } from 'react-native-sse';
import { getDefaultStore } from 'jotai';

import { apiClient } from '@/shared/api/client';
import { STORAGE_KEYS } from '@/shared/constants/storageKeys';
import { ConfigService } from '@/shared/services/Config';
import { SecureStorageService } from '@/shared/services/SecureStorage';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

import {
  xeniaBootstrapSchema,
  xeniaConversationListSchema,
  xeniaConversationSchema,
  xeniaMessageSchema,
  xeniaPreferencesSchema,
  xeniaStreamEventSchema,
} from './schemas';
import type {
  CreateXeniaConversationRequest,
  CreateXeniaMessageRequest,
  UpdateXeniaConversationRequest,
  UpdateXeniaPreferencesRequest,
  XeniaBootstrap,
  XeniaConversation,
  XeniaConversationSummary,
  XeniaMessage,
  XeniaPreferences,
  XeniaStreamCallbacks,
  XeniaStreamHandle,
} from './types';

const store = getDefaultStore();
const BASE_PATH = '/xenia/assistant';

function generateClientId(): string {
  return `mobile-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function assertCurrentApiMode(): void {
  if (store.get(apiModeAtom) === 'legacy') {
    throw new Error('Xenia AI is only available with the current API.');
  }
}

async function createSseHeaders(): Promise<Record<string, string>> {
  assertCurrentApiMode();

  const token = await SecureStorageService.getItem(STORAGE_KEYS.ACCESS_TOKEN);
  if (!token) throw new Error('Your session has expired. Please sign in again.');

  return {
    Accept: 'text/event-stream',
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
    'X-Correlation-Id': generateClientId(),
  };
}

export const xeniaKeys = {
  all: ['xenia'] as const,
  bootstrap: () => [...xeniaKeys.all, 'bootstrap'] as const,
  conversations: () => [...xeniaKeys.all, 'conversations'] as const,
  conversation: (id: string) => [...xeniaKeys.conversations(), id] as const,
};

export const XeniaApi = {
  createClientMessageId: generateClientId,

  async getBootstrap(): Promise<XeniaBootstrap> {
    assertCurrentApiMode();
    const response = await apiClient.get(`${BASE_PATH}/bootstrap`);
    return xeniaBootstrapSchema.parse(response.data);
  },

  async listConversations(): Promise<XeniaConversationSummary[]> {
    assertCurrentApiMode();
    const response = await apiClient.get(`${BASE_PATH}/conversations`);
    return xeniaConversationListSchema.parse(response.data).conversations;
  },

  async createConversation(body: CreateXeniaConversationRequest): Promise<XeniaConversation> {
    assertCurrentApiMode();
    const response = await apiClient.post(`${BASE_PATH}/conversations`, body);
    return xeniaConversationSchema.parse(response.data);
  },

  async getConversation(id: string): Promise<XeniaConversation> {
    assertCurrentApiMode();
    const response = await apiClient.get(`${BASE_PATH}/conversations/${id}`);
    return xeniaConversationSchema.parse(response.data);
  },

  async updateConversation(
    id: string,
    body: UpdateXeniaConversationRequest
  ): Promise<XeniaConversation> {
    assertCurrentApiMode();
    const response = await apiClient.patch(`${BASE_PATH}/conversations/${id}`, body);
    return xeniaConversationSchema.parse(response.data);
  },

  async archiveConversation(id: string): Promise<void> {
    assertCurrentApiMode();
    await apiClient.delete(`${BASE_PATH}/conversations/${id}`);
  },

  async createMessage(id: string, body: CreateXeniaMessageRequest): Promise<XeniaMessage> {
    assertCurrentApiMode();
    const response = await apiClient.post(`${BASE_PATH}/conversations/${id}/messages`, body);
    return xeniaMessageSchema.parse(response.data);
  },

  async getPreferences(): Promise<XeniaPreferences> {
    assertCurrentApiMode();
    const response = await apiClient.get(`${BASE_PATH}/preferences`);
    return xeniaPreferencesSchema.parse(response.data);
  },

  async updatePreferences(body: UpdateXeniaPreferencesRequest): Promise<XeniaPreferences> {
    assertCurrentApiMode();
    const response = await apiClient.patch(`${BASE_PATH}/preferences`, body);
    return xeniaPreferencesSchema.parse(response.data);
  },

  async streamMessage(
    id: string,
    body: CreateXeniaMessageRequest,
    callbacks: XeniaStreamCallbacks
  ): Promise<XeniaStreamHandle> {
    const headers = await createSseHeaders();
    const baseUrl = ConfigService.getApiBaseUrl().replace(/\/$/, '');
    const source = new EventSource<'delta' | 'completed' | 'failed'>(
      `${baseUrl}${BASE_PATH}/conversations/${id}/messages:stream`,
      {
        body: JSON.stringify(body),
        headers,
        method: 'POST',
        pollingInterval: 0,
        timeout: 65000,
      }
    );

    const parseEvent = (event: CustomEvent<string>) => {
      if (!event.data) return undefined;
      return xeniaStreamEventSchema.parse(JSON.parse(event.data));
    };

    source.addEventListener('delta', (event) => {
      try {
        const parsed = parseEvent(event);
        if (parsed?.delta) callbacks.onDelta(parsed.delta);
      } catch (error) {
        source.close();
        callbacks.onError(error instanceof Error ? error : new Error('Invalid stream response.'));
      }
    });
    source.addEventListener('completed', (event) => {
      try {
        const parsed = parseEvent(event);
        if (!parsed?.message) throw new Error('The stream completed without a message.');
        source.close();
        callbacks.onCompleted(parsed.message);
      } catch (error) {
        source.close();
        callbacks.onError(error instanceof Error ? error : new Error('Invalid stream response.'));
      }
    });
    source.addEventListener('failed', (event) => {
      try {
        const parsed = parseEvent(event);
        source.close();
        callbacks.onError(new Error(parsed?.error ?? 'Xenia could not complete the request.'));
      } catch (error) {
        source.close();
        callbacks.onError(error instanceof Error ? error : new Error('Invalid stream response.'));
      }
    });
    source.addEventListener('error', (event) => {
      source.close();
      if ('data' in event && typeof event.data === 'string') {
        try {
          const parsed = xeniaStreamEventSchema.parse(JSON.parse(event.data));
          callbacks.onError(new Error(parsed.error ?? 'Xenia could not complete the request.'));
          return;
        } catch {
          // Fall through to the transport error below.
        }
      }
      callbacks.onError(
        new Error((event as ErrorEvent).message ?? 'The Xenia stream disconnected.')
      );
    });

    return { close: () => source.close() };
  },
};
