"use client";

import { useQuery, useInfiniteQuery, keepPreviousData } from "@tanstack/react-query";
import { casesService } from "@/lib/cases/cases.service";

const CASE_SEARCH_PAGE_SIZE = 20;

export const INFINITE_CASE_SEARCH_QUERY_KEY = (keyword: string) =>
  ["case-search-infinite", keyword] as const;

/**
 * Lazy, scroll-paginated + debounced-server-search case list for the lien
 * wizard's Case picker (`@/components/selling/case-select`).
 *
 * TODO(backend): there is no lien-selling-specific case search endpoint yet.
 * This wraps the general (non-selling) Cases module's `casesService.getCases`
 * (`casesApi.listBySearch`) as a stand-in — an assumption, not a confirmed
 * Selling API contract — and should move to `@/lib/selling`'s own
 * types/api/service once a real one exists. Case *creation* already has its
 * own endpoint (`@/hooks/use-case-drafts`). Same shape as use-contacts.ts's
 * `useInfiniteContacts` / use-selling-companies.ts's `useInfiniteCompanies` —
 * only fetches a page at a time as the caller scrolls or types, pairs with
 * BaseSelect's `loadingMode="infinite"`.
 */
export function useInfiniteCasesSearch(
  keyword: string,
  options?: { enabled?: boolean },
) {
  return useInfiniteQuery({
    queryKey: INFINITE_CASE_SEARCH_QUERY_KEY(keyword),
    queryFn: ({ pageParam }) =>
      casesService.getCases({
        keyword: keyword || undefined,
        page: pageParam,
        pageSize: CASE_SEARCH_PAGE_SIZE,
      }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.pagination.page < lastPage.pagination.totalPages
        ? lastPage.pagination.page + 1
        : undefined,
    staleTime: 0,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
    // Same rationale as useInfiniteContacts: without this, reopening the
    // picker after scrolling would eagerly re-fetch every cached page.
    refetchOnMount: false,
    enabled: options?.enabled,
  });
}

export const CASE_QUERY_KEY = (id: string) => ["case", id] as const;

/**
 * Resolves a single case by id — seeds the Case picker's display label when
 * it's initialized with a value (e.g. a case just created in the case
 * wizard, or one saved on an existing lien) that isn't necessarily among
 * the currently loaded/searched page. Mirrors use-contacts.ts's `useContact`
 * / use-selling-companies.ts's `useCompany`.
 *
 * TODO(backend): also borrows the general Cases module (`casesService.getCase`)
 * for the same reason as `useInfiniteCasesSearch` above — no dedicated
 * Selling case-by-id endpoint exists yet.
 */
export function useCase(id: string | null | undefined, options?: { enabled?: boolean }) {
  const enabled = (options?.enabled ?? true) && Boolean(id);
  return useQuery({
    queryKey: CASE_QUERY_KEY(id ?? ""),
    queryFn: () => casesService.getCase(id as string),
    enabled,
    staleTime: 30_000,
    retry: false,
  });
}
