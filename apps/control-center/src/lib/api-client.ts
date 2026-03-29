/**
 * api-client.ts — Control Center server-side HTTP client.
 *
 * Wraps fetch() for use in Server Components, Server Actions, and Route
 * Handlers. Reads the platform_session JWT from the request cookie store
 * and forwards it as Authorization: Bearer on every outbound request.
 *
 * Base URL is controlled by CONTROL_CENTER_API_BASE (e.g. the API gateway).
 * Falls back to GATEWAY_URL → http://localhost:5010 if not set.
 *
 * Error handling:
 *   HTTP 401 → redirects to /login?reason=session_expired
 *   HTTP 403 → throws ApiError (Forbidden)
 *   Other non-2xx → throws ApiError with status + message
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add API caching layer (Next.js fetch cache tags)
 */

import { redirect } from 'next/navigation';
import { cookies }   from 'next/headers';

// ── Config ────────────────────────────────────────────────────────────────────

const API_BASE: string =
  process.env.CONTROL_CENTER_API_BASE ??
  process.env.GATEWAY_URL             ??
  'http://localhost:5010';

// ── Error ─────────────────────────────────────────────────────────────────────

/**
 * ApiError — thrown for any non-2xx response except 401 (which redirects).
 *
 * Carries the HTTP status code so callers can distinguish 404 (not found)
 * from 500 (server error) and render appropriate UI.
 */
export class ApiError extends Error {
  constructor(
    public readonly status:  number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }

  /** true when the admin does not have permission for the requested resource */
  get isForbidden(): boolean { return this.status === 403; }

  /** true when the requested resource does not exist */
  get isNotFound(): boolean { return this.status === 404; }

  /** true when the upstream service is unavailable */
  get isServerError(): boolean { return this.status >= 500; }
}

// ── Core ──────────────────────────────────────────────────────────────────────

/**
 * apiFetch<T>(path, options?) — send a typed HTTP request to the API gateway.
 *
 * @param path    URL path relative to API_BASE, e.g. "/identity/api/admin/tenants"
 * @param options method (default "GET"), optional request body
 *
 * On HTTP 401, redirects immediately to /login?reason=session_expired.
 * On any other non-2xx, throws ApiError.
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add API caching layer (Next.js fetch cache tags)
 */
export async function apiFetch<T>(
  path: string,
  options: {
    method?: string;
    body?:   unknown;
  } = {},
): Promise<T> {
  const cookieStore = cookies();
  const token = cookieStore.get('platform_session')?.value;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'Accept':       'application/json',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  // TODO: add correlation-id header for request tracing
  // headers['X-Correlation-Id'] = crypto.randomUUID();

  const res = await fetch(`${API_BASE}${path}`, {
    method:  options.method ?? 'GET',
    headers,
    body:    options.body !== undefined ? JSON.stringify(options.body) : undefined,
    cache:   'no-store',
  });

  // 401 — session expired or missing; redirect to login
  if (res.status === 401) {
    redirect('/login?reason=session_expired');
  }

  if (!res.ok) {
    let message = `HTTP ${res.status} ${res.statusText}`;
    try {
      const err = await res.json() as Record<string, unknown>;
      if (typeof err.message === 'string') message = err.message;
      else if (typeof err.title === 'string') message = err.title;
    } catch { /* non-JSON error body — use status text */ }
    throw new ApiError(res.status, message);
  }

  // 204 No Content
  if (res.status === 204) return undefined as T;

  return res.json() as Promise<T>;
}

// ── Convenience helpers ───────────────────────────────────────────────────────

export const apiClient = {
  get:   <T>(path: string)                => apiFetch<T>(path),
  post:  <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'POST',  body }),
  put:   <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'PUT',   body }),
  patch: <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'PATCH', body }),
  del:   <T>(path: string)               => apiFetch<T>(path, { method: 'DELETE' }),
};
