# Step 23 – Caching & Performance

## Files Updated

| File | What changed |
|------|-------------|
| `src/lib/api-client.ts` | Added `revalidateSeconds?` and `tags?` to `ApiFetchOptions`; updated `apiFetch` to apply `next: { revalidate, tags }` on GET reads or `cache: 'no-store'` on mutations; extended `apiClient.get` signature to accept optional cache params; added `CACHE_TAGS` constant map and `CacheTag` type export |
| `src/lib/control-center-api.ts` | Added `revalidateTag` import from `next/cache`; added `CACHE_TAGS` import; wired cache TTL + tags on every read call; added `revalidateTag()` after every mutation |

---

## Cache Strategy

| Endpoint | Method | Tag | TTL | Rationale |
|----------|--------|-----|-----|-----------|
| `tenants.list` | GET | `cc:tenants` | 60 s | Tenant roster changes rarely; 60 s is a good balance |
| `tenants.getById` | GET | `cc:tenants` | 60 s | Same lifecycle as list |
| `users.list` | GET | `cc:users` | 30 s | User state (invites, status) changes more often |
| `users.getById` | GET | `cc:users` | 30 s | Same lifecycle as list |
| `roles.list` | GET | `cc:roles` | 300 s | Near-static configuration; 5 min is safe |
| `roles.getById` | GET | `cc:roles` | 300 s | Same lifecycle as list |
| `audit.list` | GET | `cc:audit` | 10 s | Near-real-time log visibility for admins |
| `settings.list` | GET | `cc:settings` | 300 s | Settings change infrequently; on-demand revalidation handles updates |
| `monitoring.getSummary` | GET | `cc:monitoring` | 5 s | Live health feed; 5 s coalesces concurrent SSR requests |
| `support.list` | GET | `cc:support` | 10 s | Case status changes frequently; on-demand revalidation after mutations |
| `support.getById` | GET | `cc:support` | 10 s | Same lifecycle as list |

All mutations use `cache: 'no-store'` (never cached). This is enforced inside
`apiFetch` — the `revalidateSeconds` and `tags` options are silently ignored for
non-GET methods, so callers cannot accidentally cache a mutation.

---

## Tags Used

All tags are defined in `CACHE_TAGS` (exported from `api-client.ts`) to prevent
string literal drift across files:

```ts
export const CACHE_TAGS = {
  tenants:    'cc:tenants',
  users:      'cc:users',
  roles:      'cc:roles',
  audit:      'cc:audit',
  settings:   'cc:settings',
  monitoring: 'cc:monitoring',
  support:    'cc:support',
} as const;
```

The `cc:` prefix namespace-scopes all Control Center tags, preventing collisions
with any tags used by the main `apps/web` Next.js app on the same runtime.

---

## Revalidation Hooks

Every mutation that changes data calls `revalidateTag()` **after** the API call
succeeds (not before — the old cache is valid until the mutation completes):

| Mutation | `revalidateTag` call | Effect |
|----------|---------------------|--------|
| `tenants.updateEntitlement(...)` | `revalidateTag('cc:tenants')` | Next `tenants.list` / `getById` fetches fresh entitlement state |
| `settings.update(key, value)` | `revalidateTag('cc:settings')` | Next `settings.list` sees the updated value immediately |
| `support.create(...)` | `revalidateTag('cc:support')` | New case appears in `support.list` on next render |
| `support.addNote(caseId, msg)` | `revalidateTag('cc:support')` | Note count + case detail reflect the new note immediately |
| `support.updateStatus(caseId, status)` | `revalidateTag('cc:support')` | New status visible in both list and detail on next render |

`revalidateTag` is called from within `controlCenterServerApi` methods.
Because these methods are only called from Server Actions (where `revalidateTag`
is permitted), this pattern is safe. If a method is ever called from a Server
Component render context, Next.js will throw at runtime — which is the correct
and desirable failure mode (it surfaces the architectural mistake immediately).

---

## How apiFetch Applies Cache Config

```ts
// GET + revalidateSeconds provided → ISR cache with tags
if (isRead && options.revalidateSeconds !== undefined) {
  nextOptions = {
    revalidate: options.revalidateSeconds,
    ...(options.tags?.length ? { tags: options.tags } : {}),
  };
} else {
  // Mutation OR read with no TTL → no-store
  fetchCache = 'no-store';
}

await fetch(url, {
  ...(fetchCache  ? { cache: fetchCache } : {}),
  ...(nextOptions ? { next:  nextOptions } : {}),
});
```

The two paths are mutually exclusive — Next.js does not allow `cache` and `next`
to be set simultaneously on the same fetch call.

---

## Performance Impact

### Request deduplication (built-in)

Next.js 14 App Router deduplicates identical `fetch()` calls with the same URL
within the same render tree (per-request memoisation). With tags applied, the
cache entry is also shared across different concurrent requests during the
revalidation window, reducing upstream load.

### Avoided duplicate fetches

Pages that render both a list and a count from the same endpoint (e.g. the
tenants page showing the total badge) call `tenants.list` once. Because the
fetch is deduplicated within the render, no second request is made.

### Server-side only

All caching is in the Next.js Data Cache on the server. The browser never
receives stale data — the SSR render always sees the latest cached or fresh
response. There is no client-side SWR or stale state to manage.

### Projected load reduction (illustrative)

| Endpoint | Without caching | With caching | Reduction |
|----------|----------------|--------------|-----------|
| `roles.list` | Every SSR render | Once per 300 s | ~99% for stable role lists |
| `settings.list` | Every SSR render | Once per 300 s | ~99% for stable settings |
| `tenants.list` | Every SSR render | Once per 60 s | ~95% at 1 req/s per admin |
| `monitoring` | Every SSR render | Once per 5 s | ~80% at 1 req/s per admin |
| `audit.list` | Every SSR render | Once per 10 s | ~90% at 1 req/s per admin |

---

## TODOs

```ts
// TODO: add Redis or edge caching
//   — For multi-instance deployments, the Next.js in-process Data Cache is
//     per-instance. A shared Redis layer (or Vercel Edge Cache) would ensure
//     all instances see the same cached values and honour revalidateTag() calls
//     from any instance.

// TODO: add stale-while-revalidate strategy
//   — The current model is "revalidate in background after TTL expires"
//     (standard ISR). A stale-while-revalidate (SWR) strategy would serve
//     the stale response immediately while fetching fresh data, eliminating
//     the latency spike at the moment the cache entry expires.

// TODO: add request deduplication
//   — Next.js 14 provides built-in per-request memoisation for fetch, but
//     only for the same URL. A manual deduplication layer (e.g. a Map<string,
//     Promise<T>> keyed by URL+method) would deduplicate concurrent inflight
//     requests from different code paths within the same SSR render.
```

---

## Independence Validation

- Changes are entirely within `apps/control-center/src/lib/`
- Zero imports from `apps/web`
- No new npm dependencies — uses only:
  - `next/cache` (`revalidateTag`) — already a Next.js built-in
  - `next/navigation` (`redirect`) — already used
  - `next/headers` (`cookies`) — already used
- Public API signatures of `controlCenterServerApi.*` methods are **unchanged**
  — all call sites (pages, server actions) continue to work without modification
- `ApiError`, `apiFetch`, `apiClient` public interfaces are **backwards-compatible**
  — the new `revalidateSeconds?` and `tags?` params are optional; any code that
  calls `apiClient.get(path)` with no cache args still compiles and runs correctly
  (falls back to `cache: 'no-store'`)

---

## Any Issues or Assumptions

1. **`revalidateTag` call site constraint** — `revalidateTag` is only valid
   inside Server Actions and Route Handlers, not Server Component renders.
   All mutations in `control-center-api.ts` are only called from Server Actions
   in `app/actions/`, so this constraint is met. If a future caller invokes a
   mutation from a Server Component, Next.js will throw at runtime with a clear
   error message.

2. **Cache key includes full URL + query string** — Next.js uses the full URL
   (including query string) as the cache key. This means `tenants.list({ page: 1 })`
   and `tenants.list({ page: 2 })` are cached independently, which is correct.
   However, two calls with identical params will share the same cache entry and
   the same TTL, which is also correct.

3. **tags applied at fetch level, not at response level** — `revalidateTag` purges
   **all** cache entries that share the tag, regardless of query params. So calling
   `revalidateTag('cc:tenants')` after an entitlement update purges both
   `tenants.list(page=1)` and `tenants.list(page=2)` simultaneously. This is the
   correct, conservative behaviour for an admin tool where stale data is harmful.

4. **`monitoring.getSummary` TTL of 5 s** — This is intentionally low. If the
   monitoring page is loaded by multiple concurrent admins, Next.js will still
   deduplicate inflight requests within each render. The 5 s TTL exists only to
   coalesce requests across the revalidation window, not to cache aggressively.

5. **`CACHE_TAGS` constants are `as const`** — The object is deeply immutable.
   The `CacheTag` union type is derived from the values, so adding a new tag
   to `CACHE_TAGS` automatically widens the `CacheTag` type without any manual
   type definition.
