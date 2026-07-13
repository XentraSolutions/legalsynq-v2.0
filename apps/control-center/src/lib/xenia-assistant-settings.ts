import type { UpdateXeniaAssistantAdminSettingsPayload } from './xenia-api';

type MaybeFormValue = FormDataEntryValue | string | number | null | undefined;

export interface XeniaAssistantSettingsInput {
  provider?: MaybeFormValue;
  modelKey?: MaybeFormValue;
  openAiBaseUrl?: MaybeFormValue;
  openAiTimeoutSeconds?: MaybeFormValue;
  openAiReasoningEffort?: MaybeFormValue;
  openAiTextVerbosity?: MaybeFormValue;
  openAiMaxOutputTokens?: MaybeFormValue;
}

export function buildXeniaAssistantAdminSettingsPayload(
  input: XeniaAssistantSettingsInput,
): UpdateXeniaAssistantAdminSettingsPayload {
  const payload: UpdateXeniaAssistantAdminSettingsPayload = {
    provider: normalizeRequiredText(input.provider, 'Fake'),
    modelKey: normalizeRequiredText(input.modelKey, 'xenia-fake'),
    openAiBaseUrl: normalizeRequiredText(input.openAiBaseUrl, 'https://api.openai.com'),
    openAiTimeoutSeconds: normalizePositiveInt(input.openAiTimeoutSeconds, 60),
    openAiReasoningEffort: normalizeOptionalText(input.openAiReasoningEffort),
    openAiTextVerbosity: normalizeOptionalText(input.openAiTextVerbosity),
    openAiMaxOutputTokens: normalizeOptionalPositiveInt(input.openAiMaxOutputTokens),
  };

  return payload;
}

function normalizeRequiredText(value: MaybeFormValue, fallback: string): string {
  const normalized = String(value ?? '').trim();
  return normalized.length > 0 ? normalized : fallback;
}

function normalizePositiveInt(value: MaybeFormValue, fallback: number): number {
  const parsed = Number(String(value ?? '').trim());
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function normalizeOptionalText(value: MaybeFormValue): string | null {
  const normalized = String(value ?? '').trim();
  return normalized.length > 0 ? normalized : null;
}

function normalizeOptionalPositiveInt(value: MaybeFormValue): number | null {
  const normalized = String(value ?? '').trim();
  if (!normalized) return null;

  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}
