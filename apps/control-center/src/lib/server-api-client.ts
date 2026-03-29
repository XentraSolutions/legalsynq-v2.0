import { cookies } from 'next/headers';

const GATEWAY_URL = process.env.GATEWAY_URL ?? 'http://localhost:5010';

// ── Error ─────────────────────────────────────────────────────────────────────

export class ServerApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ServerApiError';
  }

  get isForbidden(): boolean { return this.status === 403; }
  get isNotFound():  boolean { return this.status === 404; }
}

// ── Core request ──────────────────────────────────────────────────────────────

/**
 * Server-side fetch helper for Control Center Server Components and Server Actions.
 * Reads the platform_session HttpOnly cookie and forwards it as Authorization: Bearer.
 * Never use this in Client Components — create a BFF proxy route instead.
 */
async function serverRequest<T>(
  path: string,
  options: { method?: string; body?: unknown } = {},
): Promise<T> {
  const cookieStore = cookies();
  const token = cookieStore.get('platform_session')?.value;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(`${GATEWAY_URL}${path}`, {
    method:  options.method ?? 'GET',
    headers,
    body:    options.body !== undefined ? JSON.stringify(options.body) : undefined,
    cache:   'no-store',
  });

  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const err = await res.json();
      message = err.message ?? err.title ?? message;
    } catch { /* ignore non-JSON error bodies */ }
    throw new ServerApiError(res.status, message);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

// ── Public API ────────────────────────────────────────────────────────────────

export const serverApi = {
  get:    <T>(path: string)                => serverRequest<T>(path),
  post:   <T>(path: string, body: unknown) => serverRequest<T>(path, { method: 'POST',  body }),
  put:    <T>(path: string, body: unknown) => serverRequest<T>(path, { method: 'PUT',   body }),
  patch:  <T>(path: string, body: unknown) => serverRequest<T>(path, { method: 'PATCH', body }),
  delete: <T>(path: string)               => serverRequest<T>(path, { method: 'DELETE' }),
};
