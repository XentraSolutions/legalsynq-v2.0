import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { lienPaymentsService } from "./lien-payments.service";
import type { LienPaymentQuery } from "./lien-payments.types";

export const LIEN_PAYMENTS_QUERY_KEY = (
  caseId: string,
  query?: LienPaymentQuery,
) => ["lien-payments", caseId, query ?? {}] as const;

export function useLienPayments(caseId: string, query: LienPaymentQuery) {
  return useQuery({
    queryKey: LIEN_PAYMENTS_QUERY_KEY(caseId, query),
    queryFn: () => lienPaymentsService.getLienPayments(caseId, query),
    placeholderData: keepPreviousData,
    staleTime: 1_000,
    enabled: Boolean(caseId),
    refetchOnWindowFocus: false,
  });
}
