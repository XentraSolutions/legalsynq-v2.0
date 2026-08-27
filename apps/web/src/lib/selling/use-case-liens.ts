import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { liensService } from "./selling-liens.service";
import type { LiensQuery } from "./liens.types";

export function caseLiensQueryKey(caseId: string, query?: LiensQuery) {
  return ["case-liens", caseId, query ?? {}] as const;
}

// Liens scoped to a single case — thin wrapper over liensService.getLiens
// (LiensQuery already supports `caseId`), shared by the case detail page's
// Liens/Documents/Payments tabs so they don't each re-fetch the same list
// with slightly different query shapes.
export function useCaseLiens(caseId: string, query: LiensQuery = {}) {
  return useQuery({
    queryKey: caseLiensQueryKey(caseId, query),
    queryFn: () => liensService.getLiens({ ...query, caseId }),
    placeholderData: keepPreviousData,
    enabled: Boolean(caseId),
    refetchOnWindowFocus: false,
  });
}
