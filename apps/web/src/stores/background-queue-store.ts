import { create } from 'zustand';

/**
 * App-wide priority gate: primary content (a page's main table/detail
 * fetch) registers itself while loading; non-critical background work
 * (prefetching a filter panel's option lists, warming a cache, etc.) is
 * deferred via `enqueue` so it doesn't compete with primary content for
 * network/render time, and fires the moment every currently-registered
 * primary load has resolved.
 *
 * A single shared queue (rather than one gate per page) means background
 * work anywhere in the app waits on whatever primary content is *currently*
 * loading anywhere else too — for a single-page-at-a-time app this is
 * exactly "wait for the page's own primary content," but it composes for
 * free if a layout ever has more than one primary region loading at once.
 *
 * Use the hooks in `src/hooks/use-background-queue.ts` rather than this
 * store directly.
 */
interface BackgroundQueueState {
  pendingPrimary: Set<string>;
  jobs: Array<() => void>;
  registerPrimary: (id: string) => void;
  resolvePrimary: (id: string) => void;
  enqueue: (job: () => void) => void;
}

export const useBackgroundQueueStore = create<BackgroundQueueState>((set, get) => ({
  pendingPrimary: new Set(),
  jobs: [],

  registerPrimary: (id) =>
    set((s) => {
      if (s.pendingPrimary.has(id)) return s;
      const next = new Set(s.pendingPrimary);
      next.add(id);
      return { pendingPrimary: next };
    }),

  resolvePrimary: (id) =>
    set((s) => {
      if (!s.pendingPrimary.has(id)) return s;
      const next = new Set(s.pendingPrimary);
      next.delete(id);
      if (next.size === 0 && s.jobs.length > 0) {
        const jobs = s.jobs;
        queueMicrotask(() => jobs.forEach((job) => job()));
        return { pendingPrimary: next, jobs: [] };
      }
      return { pendingPrimary: next };
    }),

  enqueue: (job) => {
    if (get().pendingPrimary.size === 0) {
      queueMicrotask(job);
    } else {
      set((s) => ({ jobs: [...s.jobs, job] }));
    }
  },
}));
