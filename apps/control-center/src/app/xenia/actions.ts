'use server';

import { revalidatePath } from 'next/cache';
import { requirePlatformAdmin } from '@/lib/auth-guards';
import { apiFetch } from '@/lib/api-client';

export interface XeniaActionResult<T = undefined> {
  success: boolean;
  error?: string;
  data?: T;
}

export interface XeniaProviderConfigurationInput {
  providerType: 'OpenAI' | 'Anthropic' | 'Gemini' | 'AzureOpenAI' | 'AwsBedrock';
  displayName: string;
  endpoint?: string | null;
  region?: string | null;
  azureDeploymentName?: string | null;
  defaultModel: string;
  allowedModels: string[];
  timeoutSeconds: number;
  retryCount: number;
  failoverPriority: number;
  enabled: boolean;
  apiKey?: string | null;
  credentialStorageMode?: 'EncryptedDatabase' | 'ExternalSecretReference';
  externalSecretReference?: string | null;
}

export interface XeniaTenantConfigurationInput {
  enabled: boolean;
  deploymentModel: 'Managed' | 'BringYourOwnAI';
  defaultProviderConfigurationId?: string | null;
  defaultModel: string;
  temperature: number;
  maxTokens: number;
  reasoningLevel: string;
  retentionPolicy: string;
  moderationPolicy: string;
  failoverEnabled: boolean;
  allowedSkills: string[];
  allowedAgents: string[];
  allowedTools: string[];
}

export async function createXeniaPlatformProvider(
  input: XeniaProviderConfigurationInput,
): Promise<XeniaActionResult<unknown>> {
  await requirePlatformAdmin();

  try {
    const data = await apiFetch('/api/xenia/admin/providers', {
      method: 'POST',
      body: input,
    });
    revalidatePath('/xenia');
    return { success: true, data };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Unable to create the Xenia provider.' };
  }
}

export async function updateXeniaPlatformProvider(
  providerConfigurationId: string,
  input: XeniaProviderConfigurationInput,
): Promise<XeniaActionResult<unknown>> {
  await requirePlatformAdmin();

  try {
    const data = await apiFetch(`/api/xenia/admin/providers/${providerConfigurationId}`, {
      method: 'PUT',
      body: input,
    });
    revalidatePath('/xenia');
    return { success: true, data };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Unable to update the Xenia provider.' };
  }
}

export async function testXeniaPlatformProvider(
  providerConfigurationId: string,
): Promise<XeniaActionResult<unknown>> {
  await requirePlatformAdmin();

  try {
    const data = await apiFetch(`/api/xenia/admin/providers/${providerConfigurationId}/test`, {
      method: 'POST',
      body: {},
    });
    revalidatePath('/xenia');
    return { success: true, data };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Unable to test the Xenia provider.' };
  }
}

export async function loadXeniaManagedConfiguration(): Promise<XeniaActionResult<unknown>> {
  await requirePlatformAdmin();

  try {
    const data = await apiFetch('/api/xenia/admin/managed-configuration', {
      method: 'GET',
    });
    return { success: true, data };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Unable to load the managed AI configuration.' };
  }
}

export async function saveXeniaManagedConfiguration(
  input: XeniaTenantConfigurationInput,
): Promise<XeniaActionResult<unknown>> {
  await requirePlatformAdmin();

  try {
    const data = await apiFetch('/api/xenia/admin/managed-configuration', {
      method: 'PUT',
      body: input,
    });
    revalidatePath('/xenia');
    return { success: true, data };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Unable to save the managed AI configuration.' };
  }
}
