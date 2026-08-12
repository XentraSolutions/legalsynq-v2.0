"use client";

import { useCallback, useEffect, useMemo } from "react";
import { useInfiniteQuery, useQueries, useQuery } from "@tanstack/react-query";
import { contactsService } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import { useSessionContext } from "@/providers/session-provider";
import type { BaseSelectOption } from "@/components/ui/base-select";
import type { ContactListResult } from "@/lib/contacts/contacts.service";

// The contacts endpoint clamps pageSize at 100 (ContactService.SearchAsync),
// so typical filter lists (~500 rows or fewer) resolve in a handful of lazy
// pages, and lists at or under 100 load in a single request. Every page is
// background-loaded (see useBackgroundInfiniteOptions below) before anyone
// starts typing, so search over these lists is plain client-side filtering
// (BaseSelect's own `filterLocally`) — no server search param needed here.

const PAGE_SIZE = 100;

export interface InfiniteOptions {
  options: BaseSelectOption[];
  isLoading: boolean;
  isFetchingMore: boolean;
  hasNextPage: boolean;
  loadMore: () => void;
  /** True once every page has been fetched — `options` is then the complete list. */
  allLoaded: boolean;
  /**
   * Fetches every remaining page (if any) and returns the complete merged
   * option list. Normally a no-op by the time it's called — pages load in
   * the background as soon as the list is enabled — this is the fallback
   * for the rare case a caller (e.g. Select All) needs the full list before
   * that background load has finished.
   */
  loadAll: () => Promise<BaseSelectOption[]>;
}

function mapPageToOptions(page: ContactListResult): BaseSelectOption[] {
  return page.items.map((c) => ({ value: c.id, label: c.displayName }));
}

function nextPageParam(last: ContactListResult): number | undefined {
  return last.pagination.page < last.pagination.totalPages
    ? last.pagination.page + 1
    : undefined;
}

/**
 * Wraps a `useInfiniteQuery` over a paginated contact-style endpoint into
 * `InfiniteOptions`. Rather than waiting for the user to scroll, every page
 * is eagerly fetched in the background right after the previous one settles
 * — for the list sizes these filters deal with (a few hundred rows at most,
 * 100/page) that finishes in a handful of requests well before anyone
 * reaches the bottom of the list. Scrolling still works as a fallback via
 * `loadMore` (e.g. if the background chain is still catching up).
 */
function useBackgroundInfiniteOptions(
  queryKey: readonly unknown[],
  queryFn: (page: number) => Promise<ContactListResult>,
  enabled: boolean,
): InfiniteOptions {
  const query = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => queryFn(pageParam),
    initialPageParam: 1,
    getNextPageParam: nextPageParam,
    enabled,
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });

  const { hasNextPage, isFetchingNextPage, isLoading, fetchNextPage } = query;
  useEffect(() => {
    if (enabled && hasNextPage && !isFetchingNextPage && !isLoading) {
      fetchNextPage();
    }
  }, [enabled, hasNextPage, isFetchingNextPage, isLoading, fetchNextPage]);

  const loadAll = useCallback(async () => {
    let result: typeof query = query;
    while (result.hasNextPage) {
      result = await result.fetchNextPage();
    }
    return (result.data?.pages ?? []).flatMap(mapPageToOptions);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.data, query.hasNextPage, query.fetchNextPage]);

  return useMemo(
    () => ({
      options: (query.data?.pages ?? []).flatMap(mapPageToOptions),
      isLoading: query.isLoading,
      isFetchingMore: query.isFetchingNextPage,
      hasNextPage: query.hasNextPage,
      loadMore: query.fetchNextPage,
      allLoaded: !query.hasNextPage && !query.isLoading,
      loadAll,
    }),
    [query, loadAll],
  );
}

/**
 * Background-loaded `{value,label}` options for a contact type — the full
 * list, unfiltered; search happens client-side once it's loaded (see the
 * module doc above).
 *
 * `mainOnly` sends an explicit blank ContactSubtype, the API convention for
 * "main contacts only" (excludes sub-contacts like facility staff) — see the
 * toQs comment in contacts.api.ts. The deployed QA backend honors this
 * (verified: staff sub-contacts drop out), but the Liens.Api source in this
 * repo (ContactEndpoints/ContactRepository) still treats a blank
 * contactSubtype as "no filter" — that implementation needs the same
 * convention before it ships, or mainOnly silently stops filtering.
 */
export function useInfiniteContactOptions(
  contactType: string,
  opts?: { enabled?: boolean; mainOnly?: boolean },
): InfiniteOptions {
  const enabled = opts?.enabled ?? true;
  return useBackgroundInfiniteOptions(
    ["contact-options", contactType, opts?.mainOnly ?? false],
    (page) =>
      contactsService.getContacts({
        ContactType: contactType,
        ContactSubtype: opts?.mainOnly ? "" : undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    enabled,
  );
}

/** Fetches every page of case managers for a single law firm, merged into one option list. */
async function fetchAllCaseManagerOptions(lawFirmId: string): Promise<BaseSelectOption[]> {
  const options: BaseSelectOption[] = [];
  let page = 1;
  for (;;) {
    const result = await contactsService.getCaseManagers({ lawFirmId, page, pageSize: PAGE_SIZE });
    options.push(...mapPageToOptions(result));
    if (result.pagination.page >= result.pagination.totalPages) break;
    page++;
  }
  return options;
}

/**
 * Same as useInfiniteContactOptions, for case managers (law-firm sub-contacts).
 *
 * The contacts endpoint only accepts a single `lawFirmId` per request (no
 * array param), so when the caller scopes to more than one firm this fires
 * one query per firm (via `useQueries`, each looping its own pages) and
 * merges the results — rather than the single background-paginated list
 * `useBackgroundInfiniteOptions` uses for the unscoped case.
 */
export function useInfiniteCaseManagerOptions(
  opts?: { enabled?: boolean; lawFirmIds?: string[] },
): InfiniteOptions {
  const enabled = opts?.enabled ?? true;
  const lawFirmIds = opts?.lawFirmIds ?? [];
  const scoped = lawFirmIds.length > 0;

  const unscoped = useBackgroundInfiniteOptions(
    ["case-manager-options", null],
    (page) => contactsService.getCaseManagers({ page, pageSize: PAGE_SIZE }),
    enabled && !scoped,
  );

  const scopedQueries = useQueries({
    queries: lawFirmIds.map((lawFirmId) => ({
      queryKey: ["case-manager-options", lawFirmId],
      queryFn: () => fetchAllCaseManagerOptions(lawFirmId),
      enabled: enabled && scoped,
      staleTime: 30_000,
      refetchOnWindowFocus: false,
    })),
  });

  return useMemo(() => {
    if (!scoped) return unscoped;

    const isLoading = scopedQueries.some((q) => q.isLoading);
    const merged = new Map<string, BaseSelectOption>();
    for (const q of scopedQueries) {
      for (const option of q.data ?? []) merged.set(option.value, option);
    }
    const options = Array.from(merged.values());

    return {
      options,
      isLoading,
      isFetchingMore: false,
      hasNextPage: false,
      loadMore: () => {},
      allLoaded: !isLoading,
      loadAll: async () => options,
    };
  }, [scoped, unscoped, scopedQueries]);
}

/** Lien status lookup — small, single-request list, wrapped in the same shape as the paginated sources above. */
export function useLienStatusOptions(opts?: { enabled?: boolean }): InfiniteOptions {
  const query = useQuery({
    queryKey: ["lien-status-options"],
    queryFn: () => lookupService.getLiensStatus(),
    enabled: opts?.enabled ?? true,
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });

  const options = useMemo<BaseSelectOption[]>(
    () => (query.data?.items ?? []).map((s) => ({ value: s.code, label: s.name })),
    [query.data],
  );

  return useMemo(
    () => ({
      options,
      isLoading: query.isLoading,
      isFetchingMore: false,
      hasNextPage: false,
      loadMore: () => {},
      allLoaded: !query.isLoading,
      loadAll: async () => options,
    }),
    [options, query.isLoading],
  );
}

/**
 * Accident type options for the cases filter — sourced from the session's
 * `/lookup/all` payload (already fetched once at session start, see
 * session-provider.tsx), so this is a synchronous wrap rather than its own
 * query. Uses the lookup row's `id` as the option value: verified directly
 * against the cases v3 endpoint — `accidentTypeId` matches against
 * CaseResponse.accidentTypeId (a GUID FK), and passing the `code` string
 * instead (e.g. "DogBite") returns zero results despite matching cases
 * existing. The pre-existing CasesFilter used `code` here, which had the
 * same bug.
 */
export function useAccidentTypeOptions(): InfiniteOptions {
  const { lookup } = useSessionContext();

  const options = useMemo<BaseSelectOption[]>(
    () => (lookup?.AccidentType ?? []).map((a) => ({ value: a.id, label: a.name })),
    [lookup],
  );

  return useMemo(
    () => ({
      options,
      isLoading: false,
      isFetchingMore: false,
      hasNextPage: false,
      loadMore: () => {},
      allLoaded: true,
      loadAll: async () => options,
    }),
    [options],
  );
}

/**
 * Case status options for the cases filter — same source as
 * useAccidentTypeOptions, but `code` is correct here: verified against the
 * cases v3 endpoint, `statusId` matches against Case.status directly (a
 * string code like "PreDemand"), unlike accidentTypeId's GUID FK.
 */
export function useCaseStatusOptions(): InfiniteOptions {
  const { lookup } = useSessionContext();

  const options = useMemo<BaseSelectOption[]>(
    () => (lookup?.CaseStatus ?? []).map((s) => ({ value: s.code, label: s.name })),
    [lookup],
  );

  return useMemo(
    () => ({
      options,
      isLoading: false,
      isFetchingMore: false,
      hasNextPage: false,
      loadMore: () => {},
      allLoaded: true,
      loadAll: async () => options,
    }),
    [options],
  );
}
