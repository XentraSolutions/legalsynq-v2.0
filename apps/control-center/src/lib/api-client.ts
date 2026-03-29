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
 * Caching (GET requests only):
 *   - Pass revalidateSeconds to opt into Next.js ISR-style fetch caching.
 *   - Pass tags[] to enable on-demand revalidation via revalidateTag().
 *   - Mutations (POST/PATCH/PUT/DELETE) always use cache: 'no-store'.
 *   - Reads with no revalidateSeconds default to cache: 'no-store' (safe default).
 *
 * Error handling:
 *   HTTP 401 → redirects to /login?reason=session_expired
 *   HTTP 403 → throws ApiError (Forbidden)
 *   Other non-2xx → throws ApiError with status + message
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
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

// ── Options ───────────────────────────────────────────────────────────────────

/**
 * ApiFetchOptions — extended options for apiFetch.
 *
 * method            — HTTP verb (default "GET")
 * body              — JSON-serialisable request body
 * revalidateSeconds — seconds until the Next.js Data Cache entry expires.
 *                     Only applied on GET requests; ignored on mutations.
 *                     Pass 0 to force re-fetch on every request (same as
 *                     cache: 'no-store' but keeps the cache entry warm for
 *                     on-demand revalidation via tags).
 * tags              — cache tags for on-demand revalidation via revalidateTag().
 *                     Only applied on GET requests.
 *
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */
export interface ApiFetchOptions {
  method?:            string;
  body?:              unknown;
  revalidateSeconds?: number;
  tags?:              string[];
}

// ── Core ──────────────────────────────────────────────────────────────────────

/**
 * apiFetch<T>(path, options?) — send a typed HTTP request to the API gateway.
 *
 * Caching behaviour:
 *   GET + revalidateSeconds set → next: { revalidate, tags }
 *   GET + no revalidateSeconds  → cache: 'no-store'
 *   Non-GET                     → cache: 'no-store' (never cache mutations)
 *
 * On HTTP 401, redirects immediately to /login?reason=session_expired.
 * On any other non-2xx, throws ApiError.
 *
 * TODO: add retry/backoff
 * TODO: add request tracing (correlation-id header)
 * TODO: add Redis or edge caching
 * TODO: add stale-while-revalidate strategy
 * TODO: add request deduplication
 */
export async function apiFetch<T>(
  path:    string,
  options: ApiFetchOptions = {},
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

  const method = options.method ?? 'GET';
  const isRead = method === 'GET' || method === 'HEAD';

  // Build the Next.js fetch cache config:
  //   - Reads with revalidateSeconds → ISR-style with optional tags
  //   - Everything else             → no-store (never cache mutations)
  let fetchCache: RequestCache | undefined;
  let nextOptions: { revalidate?: number; tags?: string[] } | undefined;

  if (isRead && options.revalidateSeconds !== undefined) {
    // Use Next.js data cache with revalidation
    nextOptions = {
      revalidate: options.revalidateSeconds,
      ...(options.tags && options.tags.length > 0 ? { tags: options.tags } : {}),
    };
  } else {
    fetchCache = 'no-store';
  }

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body:    options.body !== undefined ? JSON.stringify(options.body) : undefined,
    ...(fetchCache     ? { cache: fetchCache }    : {}),
    ...(nextOptions    ? { next:  nextOptions }    : {}),
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
  /**
   * GET with optional Next.js cache config.
   *
   * @param path              URL path (relative to API_BASE)
   * @param revalidateSeconds seconds until cache expires (omit → no-store)
   * @param tags              cache tags for revalidateTag() on-demand purge
   */
  get: <T>(
    path:              string,
    revalidateSeconds?: number,
    tags?:              string[],
  ) => apiFetch<T>(path, { revalidateSeconds, tags }),

  /** POST — always cache: 'no-store' */
  post:  <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'POST',  body }),

  /** PUT — always cache: 'no-store' */
  put:   <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'PUT',   body }),

  /** PATCH — always cache: 'no-store' */
  patch: <T>(path: string, body: unknown) => apiFetch<T>(path, { method: 'PATCH', body }),

  /** DELETE — always cache: 'no-store' */
  del:   <T>(path: string)               => apiFetch<T>(path, { method: 'DELETE' }),
};

// ── Cache tag constants ───────────────────────────────────────────────────────

/**
 * CACHE_TAGS — canonical tag strings used with revalidateTag().
 *
 * Import these in control-center-api.ts (for applying to fetch calls)
 * and in any Server Action that mutates data (for calling revalidateTag).
 *
 * TODO: add Redis or edge caching (tags would serve as Redis key prefixes)
 */
export const CACHE_TAGS = {
  tenants:    'cc:tenants',
  users:      'cc:users',
  roles:      'cc:roles',
  audit:      'cc:audit',
  settings:   'cc:settings',
  monitoring: 'cc:monitoring',
  support:    'cc:support',
} as const;

export type CacheTag = typeof CACHE_TAGS[keyof typeof CACHE_TAGS];
