"use client";

import { useQuery } from "@tanstack/react-query";
import { liensService, type LienListItem } from "@/lib/liens";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";

export type CaseLienRow = CaseLienItem & CaseLienItemMetadata;

async function fetchCaseLiens(caseId: string): Promise<CaseLienRow[]> {
  const result = await liensService.getLiens({ caseId }).catch(() => ({
    items: [] as LienListItem[],
    pagination: { page: 1, pageSize: 50, totalCount: 0, totalPages: 0 },
  }));

  return result.items.map((lien) => {
    const ext = lien as LienListItem & CaseLienItemMetadata;
    return {
      ...lien,
      facility: ext.facility ?? "(Blank)",
      originalAmount: ext.originalAmount ?? 0,
      reductionAmount: ext.reductionAmount ?? null,
      purchaseAmount: ext.purchaseAmount ?? null,
      paymentAmount: ext.paymentAmount ?? null,
      balance:
        (ext.originalAmount ?? 0) -
        (ext.reductionAmount ?? 0) -
        (ext.paymentAmount ?? 0),
      closedAtUtc: ext.closedAtUtc ?? null,
    };
  });
}

export function useCaseLiens(caseId: string) {
  return useQuery({
    queryKey: ["case-liens", caseId],
    queryFn: () => fetchCaseLiens(caseId),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}
