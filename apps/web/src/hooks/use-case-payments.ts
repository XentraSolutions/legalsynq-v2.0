"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { settlementService } from "@/lib/settlement";
import type { CasePaymentQuery } from "@/lib/settlement/settlement.types";

export const CASE_PAYMENT_LEDGER_QUERY_KEY = (
  caseId: string,
  query?: CasePaymentQuery,
) => ["case-payment-ledger", caseId, query ?? {}] as const;

export function useCasePayments(caseId: string, query: CasePaymentQuery) {
  return useQuery({
    queryKey: CASE_PAYMENT_LEDGER_QUERY_KEY(caseId, query),
    queryFn: () => settlementService.getCasePayments(caseId, query),
    placeholderData: keepPreviousData,
    staleTime: 30_000,
    enabled: Boolean(caseId),
    refetchOnWindowFocus: false,
  });
}
