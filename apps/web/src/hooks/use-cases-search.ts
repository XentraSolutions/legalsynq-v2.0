"use client";

import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { casesService } from "@/lib/cases/cases.service";
import type { CaseListItem } from "@/lib/cases/cases.types";

const CASE_SEARCH_PAGE_SIZE = 20;

export const CASE_SEARCH_QUERY_KEY = (keyword: string) =>
  ["case-search", keyword] as const;

/**
 * Debounced-server-search case list for the lien wizard's Case picker
 * (`@/components/selling/case-select`). Wraps the existing
 * casesService.getCases (casesApi.listBySearch), the same lookup already
 * used elsewhere for case search — this is a read-only query, distinct from
 * case *creation*, which isn't wired to a real endpoint yet.
 */
export function useCasesSearch(
  keyword: string,
  options?: { enabled?: boolean },
) {
  const query = useQuery({
    queryKey: CASE_SEARCH_QUERY_KEY(keyword),
    queryFn: () =>
      casesService.getCases({
        keyword: keyword || undefined,
        page: 1,
        pageSize: CASE_SEARCH_PAGE_SIZE,
      }),
    enabled: options?.enabled,
    placeholderData: keepPreviousData,
    staleTime: 15_000,
  });

  return {
    ...query,
    items: query.data?.items ?? ([] as CaseListItem[]),
  };
}
