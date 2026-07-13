import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildXeniaAssistantAdminSettingsPayload } from '../xenia-assistant-settings';

test('buildXeniaAssistantAdminSettingsPayload preserves the new OpenAI tuning fields', () => {
  const payload = buildXeniaAssistantAdminSettingsPayload({
    provider: 'OpenAI',
    modelKey: 'gpt-5',
    openAiBaseUrl: 'https://api.openai.com',
    openAiTimeoutSeconds: '120',
    openAiReasoningEffort: 'high',
    openAiTextVerbosity: 'medium',
    openAiMaxOutputTokens: '4096',
  });

  assert.deepEqual(payload, {
    provider: 'OpenAI',
    modelKey: 'gpt-5',
    openAiBaseUrl: 'https://api.openai.com',
    openAiTimeoutSeconds: 120,
    openAiReasoningEffort: 'high',
    openAiTextVerbosity: 'medium',
    openAiMaxOutputTokens: 4096,
  });
});

test('buildXeniaAssistantAdminSettingsPayload clears blank optional OpenAI tuning fields', () => {
  const payload = buildXeniaAssistantAdminSettingsPayload({
    provider: ' OpenAI ',
    modelKey: ' gpt-5 ',
    openAiBaseUrl: ' https://api.openai.com ',
    openAiTimeoutSeconds: '',
    openAiReasoningEffort: ' ',
    openAiTextVerbosity: '',
    openAiMaxOutputTokens: '0',
  });

  assert.deepEqual(payload, {
    provider: 'OpenAI',
    modelKey: 'gpt-5',
    openAiBaseUrl: 'https://api.openai.com',
    openAiTimeoutSeconds: 60,
    openAiReasoningEffort: null,
    openAiTextVerbosity: null,
    openAiMaxOutputTokens: null,
  });
});
