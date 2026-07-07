"use client";

import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { settlementService } from "@/lib/settlement";
import type { SettlementHistoryV3Query } from "@/lib/settlement/settlement.types";

export const SETTLEMENT_HISTORY_QUERY_KEY = (query: SettlementHistoryV3Query) =>
  ["settlement-history", query] as const;

export function useSettlementHistory(
  query: SettlementHistoryV3Query,
  options: { enabled?: boolean } = {},
) {
  return useQuery({
    queryKey: SETTLEMENT_HISTORY_QUERY_KEY(query),
    queryFn: () => settlementService.getSettlementHistoryV3(query),
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
    enabled: !!query.caseId && options.enabled !== false,
  });
}
