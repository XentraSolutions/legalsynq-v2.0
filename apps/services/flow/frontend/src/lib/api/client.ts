import { config } from "@/lib/config";

const TIMEOUT_MS = 10000;
const TENANT_HEADER = "X-Tenant-Id";
const DEFAULT_TENANT = "default";

export function getTenantId(): string {
  if (typeof window !== "undefined") {
    return localStorage.getItem("flow_tenant_id") || DEFAULT_TENANT;
  }
  return DEFAULT_TENANT;
}

export function setTenantId(tenantId: string): void {
  if (typeof window !== "undefined") {
    localStorage.setItem("flow_tenant_id", tenantId);
  }
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const url = `${config.apiBaseUrl}${path}`;
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), TIMEOUT_MS);

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    [TENANT_HEADER]: getTenantId(),
  };

  if (options?.headers) {
    const incoming = options.headers as Record<string, string>;
    Object.assign(headers, incoming);
  }

  let res: Response;
  try {
    res = await Promise.race([
      fetch(url, {
        ...options,
        headers,
        signal: controller.signal,
      }),
      new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error("Backend unavailable (request timed out)")), TIMEOUT_MS)
      ),
    ]);
  } catch (err) {
    clearTimeout(timeoutId);
    if (err instanceof Error && err.message.includes("Backend unavailable")) {
      throw err;
    }
    throw new Error("Backend unavailable");
  } finally {
    clearTimeout(timeoutId);
  }

  if (res.status === 204) return undefined as T;

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    const message =
      body?.error || body?.errors?.join(", ") || `Request failed (${res.status})`;
    throw new Error(message);
  }

  return res.json();
}
