const XENIA_BASE = process.env.XENIA_API_BASE ?? 'http://127.0.0.1:5035';

export interface XeniaServiceInfo {
  service: string;
  version: string;
  environment: string;
  started_at: string;
  uptime_seconds: number;
  is_standalone: boolean;
}

export interface XeniaModuleDto {
  id: string;
  module_key: string;
  name: string;
  version: string;
  description: string;
  global_enabled: boolean;
  status: string;
  configuration_namespace: string;
  created_at_utc: string;
  updated_at_utc: string;
}

export interface XeniaAdapterDto {
  id: string;
  adapter_key: string;
  adapter_type: string;
  name: string;
  version: string;
  configuration_status: string;
  availability_status: string;
  health_status: string;
  last_health_check_at: string | null;
  diagnostic_message: string | null;
}

export interface XeniaHealthResponse {
  status: string;
  service: string;
  timestamp: string;
}

export interface XeniaReadyResponse {
  status: string;
  checks: Record<string, unknown>;
}

async function fetchXenia<T>(path: string, token?: string): Promise<T | null> {
  try {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (token) headers['Authorization'] = `Bearer ${token}`;

    const res = await fetch(`${XENIA_BASE}${path}`, {
      headers,
      cache: 'no-store',
    });

    if (!res.ok) return null;
    return res.json() as Promise<T>;
  } catch {
    return null;
  }
}

export async function getXeniaInfo(): Promise<XeniaServiceInfo | null> {
  return fetchXenia<XeniaServiceInfo>('/info');
}

export async function getXeniaHealth(): Promise<XeniaHealthResponse | null> {
  return fetchXenia<XeniaHealthResponse>('/health');
}

export async function getXeniaReady(): Promise<XeniaReadyResponse | null> {
  return fetchXenia<XeniaReadyResponse>('/ready');
}

export async function getXeniaModules(token: string): Promise<XeniaModuleDto[]> {
  const res = await fetchXenia<{ modules: XeniaModuleDto[]; total: number }>(
    '/modules',
    token,
  );
  return res?.modules ?? [];
}

export async function getXeniaAdapters(token: string): Promise<XeniaAdapterDto[]> {
  const res = await fetchXenia<{ adapters: XeniaAdapterDto[]; total: number }>(
    '/adapters',
    token,
  );
  return res?.adapters ?? [];
}
