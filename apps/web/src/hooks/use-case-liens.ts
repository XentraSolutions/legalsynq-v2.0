"use client";

import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import { LiensQuery, liensService, type LienListItem } from "@/lib/liens";
import {
  CasesQuery,
  casesService,
  CreateCaseRequestDto,
  type CaseLienItem,
  type CaseLienItemMetadata,
} from "@/lib/cases";
import { settlementService } from "@/lib/settlement";
import type { CasePayment } from "@/lib/settlement/settlement.types";
import { lookupService } from "@/lib/lookup";
import { PaginationMeta } from "@/lib/contacts";
import { servicingApi } from "@/lib/servicing/servicing.api";

export type CaseLienRow = CaseLienItem & CaseLienItemMetadata;

export const CASE_PAYMENTS_QUERY_KEY = (caseId: string) =>
  ["case-payments", caseId] as const;

export function useLienPaymentsByCase(caseId: string) {
  return useQuery({
    queryKey: CASE_PAYMENTS_QUERY_KEY(caseId),
    queryFn: () =>
      settlementService
        .getLienPaymentsByCase(caseId)
        .catch(() => [] as CasePayment[]),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

async function fetchCaseLiens(
  caseId: string,
  query: LiensQuery,
  queryClient: QueryClient,
): Promise<CaseLienRow[]> {
  const [liensResult, payments, reductions, facilities] = await Promise.all([
    liensService.getLiens({ caseId, ...query }).catch(() => ({
      items: [] as LienListItem[],
      pagination: { page: 1, pageSize: 50, totalCount: 0, totalPages: 0 },
    })),
    // Reuse the cached payments query if already fetched; otherwise fetch now
    queryClient.ensureQueryData({
      queryKey: CASE_PAYMENTS_QUERY_KEY(caseId),
      queryFn: () =>
        settlementService
          .getLienPaymentsByCase(caseId)
          .catch(() => [] as CasePayment[]),
    }),
    settlementService.getLienReductionsByCase(caseId).catch(() => []),
    lookupService.getMedicalFacility().catch(() => ({
      items: [],
    })),
  ]);

  // Sum all payments per lienId (amount may come back as a string from the legacy endpoint)
  const paymentsByLien = new Map<string, number>();
  for (const p of payments) {
    const amt = parseFloat(String(p.amount)) || 0;
    paymentsByLien.set(p.lienId, (paymentsByLien.get(p.lienId) ?? 0) + amt);
  }

  // Keep only the latest reduction per lienId (by reductionDate desc, then createdAtUtc desc)
  const latestReductionByLien = new Map<string, number>();
  const sortedReductions = [...reductions].sort((a, b) => {
    const dateDiff = b.reductionDate.localeCompare(a.reductionDate);
    return dateDiff !== 0
      ? dateDiff
      : b.createdAtUtc.localeCompare(a.createdAtUtc);
  });
  for (const r of sortedReductions) {
    if (!latestReductionByLien.has(r.lienId)) {
      latestReductionByLien.set(r.lienId, r.amount);
    }
  }
  function facilityName(facilityId: string | null) {
    if (!facilityId) return "";
    return facilities.items.find((f) => f.id == facilityId)?.name;
  }

  return liensResult.items.map((lien) => {
    const ext = lien as LienListItem & CaseLienItemMetadata;
    const paymentAmount =
      paymentsByLien.get(lien.id) ?? ext.paymentAmount ?? null;
    const reductionAmount =
      latestReductionByLien.get(lien.id) ?? ext.reductionAmount ?? null;
    const originalAmount = ext.originalAmount ?? 0;
    return {
      ...lien,
      facility: ext.facility ?? "(Blank)",
      facilityName: facilityName(ext.facilityId ?? "") ?? "(Blank)",
      serviceDate: ext.initialServiceDate,
      purchaseDateDate: ext.purchaseDate,
      originalAmount,
      reductionAmount,
      purchaseAmount: ext.purchaseAmount ?? 0,
      paymentAmount,
      balance: originalAmount - (reductionAmount ?? 0) - (paymentAmount ?? 0),
      closedAtUtc: ext.closedAtUtc ?? null,
    };
  });
}

export function useCaseLiens(caseId: string, query: LiensQuery) {
  const queryClient = useQueryClient();
  return useQuery({
    queryKey: ["case-liens", caseId],
    queryFn: () => fetchCaseLiens(caseId, query, queryClient),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

export function useCases(query: CasesQuery) {
  return useQuery({
    queryKey: ["cases", query],
    queryFn: () => casesService.getCases(query),
    staleTime: 30_000,
  });
}

export function useCreateCase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateCaseRequestDto) =>
      casesService.createCase(payload),
    onSuccess: (data) => {
      queryClient.invalidateQueries({
        queryKey: ["cases"],
      });
    },
  });
}
