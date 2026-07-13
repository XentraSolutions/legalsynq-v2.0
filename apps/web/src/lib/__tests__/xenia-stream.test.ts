import assert from 'node:assert/strict';
import { test } from 'node:test';
import { normalizeStreamEvent } from '../xenia/stream';

test('normalizeStreamEvent accepts PascalCase SSE payloads from Xenia', () => {
  const evt = normalizeStreamEvent({
    Type: 'error',
    Delta: null,
    Message: null,
    Error: 'OpenAI API key is not configured in Xenia appsettings.',
  });

  assert.deepEqual(evt, {
    type: 'error',
    delta: null,
    message: null,
    error: 'OpenAI API key is not configured in Xenia appsettings.',
  });
});

test('normalizeStreamEvent normalizes nested PascalCase assistant messages', () => {
  const evt = normalizeStreamEvent({
    Type: 'message',
    Message: {
      Id: 'msg-1',
      ConversationId: 'conv-1',
      Role: 'Assistant',
      Content: 'READY',
      Provider: 'openai',
      ProviderResponseId: 'resp-1',
      InputTokens: 11,
      OutputTokens: 7,
      FinishReason: 'stop',
      CreatedAtUtc: '2026-07-13T16:13:13.797333Z',
      Citations: [
        {
          Id: 'citation-1',
          SourceType: 'document',
          SourceId: 'doc-1',
          Label: 'Tenant policy',
          Url: 'https://example.com/doc-1',
        },
      ],
    },
  });

  assert.deepEqual(evt, {
    type: 'message',
    delta: null,
    error: null,
    message: {
      id: 'msg-1',
      conversationId: 'conv-1',
      role: 'assistant',
      content: 'READY',
      provider: 'openai',
      providerResponseId: 'resp-1',
      inputTokens: 11,
      outputTokens: 7,
      finishReason: 'stop',
      createdAtUtc: '2026-07-13T16:13:13.797333Z',
      citations: [
        {
          id: 'citation-1',
          sourceType: 'document',
          sourceId: 'doc-1',
          label: 'Tenant policy',
          url: 'https://example.com/doc-1',
        },
      ],
    },
  });
});
