"use client";

import { useEffect, useId } from "react";
import { useBackgroundQueueStore } from "@/stores/background-queue-store";

/**
 * Registers a primary content loading flag (e.g. a page's main table fetch)
 * with the app-wide background queue. While `pending` is true, any
 * background work deferred via `useEnqueueBackground`/`useBackgroundReady`
 * anywhere in the app waits for it.
 *
 * Registration happens on mount/update via `useEffect`, same as any other
 * external-store write — like all `useSyncExternalStore`-backed state
 * (zustand included), a fresh registration takes a render or two to
 * propagate to every subscriber, so `useBackgroundReady()` alone has a
 * brief eventual-consistency window right on first mount. If the
 * background consumer lives in the same component (or a direct child) as
 * the primary loading flag, skip that window entirely by combining the
 * flag directly — see `useBackgroundReady`'s doc.
 *
 * @example
 * ```tsx
 * const { isLoading } = useLiensQuery();
 * usePrimaryLoad(isLoading);
 * ```
 */
export function usePrimaryLoad(pending: boolean): void {
  const id = useId();
  const registerPrimary = useBackgroundQueueStore((s) => s.registerPrimary);
  const resolvePrimary = useBackgroundQueueStore((s) => s.resolvePrimary);

  useEffect(() => {
    if (pending) registerPrimary(id);
    else resolvePrimary(id);
  }, [pending, id, registerPrimary, resolvePrimary]);

  // Unregister if the component unmounts mid-load, so it can't wedge the
  // queue open forever.
  useEffect(() => () => resolvePrimary(id), [id, resolvePrimary]);
}

/**
 * True once every currently-registered primary load has resolved (true if
 * none are registered).
 *
 * On the very first render after a primary load starts, this can briefly
 * read `true` before that load's own `usePrimaryLoad` registration has
 * propagated (the usual external-store lag — see `usePrimaryLoad`'s doc).
 * If you have direct access to the primary flag(s) yourself (the common
 * case: a page gating its own filter panel on its own table load), close
 * that window by combining it locally instead of trusting this alone:
 *
 * ```tsx
 * usePrimaryLoad(loading);
 * const ready = useBackgroundReady() && !loading;
 * ```
 *
 * Consumers with no such direct access (a background job in an unrelated
 * part of the tree) can use this on its own — the propagation window is a
 * render or two, not a meaningful delay for non-critical work.
 */
export function useBackgroundReady(): boolean {
  return useBackgroundQueueStore((s) => s.pendingPrimary.size === 0);
}

/**
 * Returns a stable function that runs a job immediately if the queue is
 * already idle, otherwise defers it until every registered primary load
 * resolves. For work that isn't naturally a TanStack Query `enabled` flag
 * (an imperative prefetch call, an analytics ping, etc) — most consumers
 * should just read `useBackgroundReady()` and feed it straight into
 * `enabled` instead.
 *
 * @example
 * ```tsx
 * const enqueue = useEnqueueBackground();
 * useEffect(() => { enqueue(() => queryClient.prefetchQuery(reportOptions)); }, [enqueue]);
 * ```
 */
export function useEnqueueBackground(): (job: () => void) => void {
  return useBackgroundQueueStore((s) => s.enqueue);
}
