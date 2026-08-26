"use client";

import {
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from "@tanstack/react-query";
import {
  LiensQuery,
  liensService,
  PaginationMeta,
  type LienListItem,
} from "@/lib/liens";
import {
  CasesQuery,
  casesService,
  CreateCaseRequestDto,
  type CaseLienItem,
  type CaseLienItemMetadata,
} from "@/lib/cases";
import { settlementService } from "@/lib/settlement";
import type {
  CasePayment,
  LegacyCasePayment,
} from "@/lib/settlement/settlement.types";
import { contactsService } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import {
  servicingService,
  type UpdateServicingDetailsRequestDto,
} from "@/lib/servicing";
import { dateConverter } from "@/lib/cases/cases.mapper";
import { CreateMedicalCodeLiensDto } from "@/lib/cases/cases.types";

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

export const SETTLEMENT_PAYMENT_DETAILS_QUERY_KEY = (caseId: string) =>
  ["settlement-payment-details", caseId] as const;

export function useSettlementPaymentDetails(caseId: string) {
  return useQuery({
    queryKey: SETTLEMENT_PAYMENT_DETAILS_QUERY_KEY(caseId),
    queryFn: () =>
      settlementService
        .getSettlementPaymentDetails(caseId)
        .catch(() => [] as LegacyCasePayment[]),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

async function fetchLienPage(
  caseId: string,
  query: LiensQuery,
): Promise<{ items: LienListItem[]; pagination: PaginationMeta }> {
  return liensService.getLiens({ caseId, ...query }).catch(() => ({
    items: [] as LienListItem[],
    pagination: {
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
      totalCount: 0,
      totalPages: 0,
    },
  }));
}

// Server-enforced page size ceiling — requesting more than this per page is a no-op.
const LIENS_PAGE_SIZE = 100;
// Safety cap on how many pages fetchAllLienPages will walk (~2000 liens for one case).
// Guards against runaway fetching if pagination metadata is ever wrong; a case that
// legitimately exceeds this will silently show a partial set (logged via console.warn).
const MAX_LIEN_PAGES = 20;

/**
 * TODO(liens API): there is no case-scoped "all liens" endpoint and no way to filter
 * by open/closed status server-side, so the servicing tab (which needs every lien on
 * the case, split into open/closed client-side) has to page through the capped list
 * endpoint itself and concatenate. This is fine for realistic per-case lien counts but
 * is a workaround — replace with a real endpoint (uncapped "all liens for case", or
 * status-filterable server pagination) when available.
 */
async function fetchAllLienPages(
  caseId: string,
  query: LiensQuery,
): Promise<LienListItem[]> {
  const first = await fetchLienPage(caseId, {
    ...query,
    page: 1,
    pageSize: LIENS_PAGE_SIZE,
  });
  const totalPages = first.pagination.totalPages;

  if (totalPages > MAX_LIEN_PAGES) {
    console.warn(
      `[use-case-liens] case ${caseId} has ${totalPages} lien pages; only loading the first ${MAX_LIEN_PAGES} (${MAX_LIEN_PAGES * LIENS_PAGE_SIZE} liens). See TODO in use-case-liens.ts.`,
    );
  }

  if (totalPages <= 1) return first.items;

  const pagesToFetch = Math.min(totalPages, MAX_LIEN_PAGES);
  const remainingPages = Array.from(
    { length: pagesToFetch - 1 },
    (_, i) => i + 2,
  );
  const rest = await Promise.all(
    remainingPages.map((page) =>
      fetchLienPage(caseId, { ...query, page, pageSize: LIENS_PAGE_SIZE }),
    ),
  );

  return [first.items, ...rest.map((r) => r.items)].flat();
}

async function enrichLiens(
  caseId: string,
  liens: LienListItem[],
  queryClient: QueryClient,
): Promise<CaseLienRow[]> {
  const [payments, reductions, facilities] = await Promise.all([
    // Reuse the cached payments query if it's still fresh; otherwise fetch now.
    // Must be fetchQuery (not ensureQueryData) — ensureQueryData returns whatever
    // is cached whenever data is already present, ignoring invalidateQueries, so a
    // payment/reduction save would never be reflected in the recomputed balance.
    queryClient.fetchQuery({
      queryKey: CASE_PAYMENTS_QUERY_KEY(caseId),
      queryFn: () =>
        settlementService
          .getLienPaymentsByCase(caseId)
          .catch(() => [] as CasePayment[]),
    }),
    settlementService.getLienReductionsByCase(caseId).catch(() => []),
    contactsService
      .getContacts({ ContactType: "MedicalFacility" })
      .catch(() => ({
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
  const latestReductionDateByLien = new Map<string, string>();

  const sortedReductions = [...reductions].sort((a, b) => {
    const dateDiff = b.reductionDate.localeCompare(a.reductionDate);
    return dateDiff !== 0
      ? dateDiff
      : b.createdAtUtc.localeCompare(a.createdAtUtc);
  });
  for (const r of sortedReductions) {
    if (!latestReductionByLien.has(r.lienId)) {
      latestReductionByLien.set(r.lienId, r.amount);
      latestReductionDateByLien.set(r.lienId, dateConverter(r.reductionDate));
    }
  }
  function facilityName(facilityId: string | null) {
    return facilities.items.find((f) => f.id == facilityId)?.displayName;
  }
  return liens.map((lien) => {
    // paymentAmount/reductionAmount only ever come from the payments/reductions
    // endpoints fetched above — LienListItem (the raw lien list row) has no such
    // fields, so there is no fallback to them here.
    const paymentAmount = paymentsByLien.get(lien.id) ?? null;
    const reductionAmount = latestReductionByLien.get(lien.id) ?? null;
    const reductionDate = latestReductionDateByLien.get(lien.id) ?? null;
    const originalAmount = lien.totalBilling ?? 0;
    const totalBalance =
      originalAmount - (reductionAmount ?? 0) - (paymentAmount ?? 0);
    // A lien can bundle multiple medical billing line items; totalBilling is
    // the server-aggregated sum across those. The DTO also has a legacy
    // single-value originalAmount field, but we don't consume it on the
    // client — the aggregate is the only "billing amount" this view needs.
    return {
      ...lien,
      facility: lien.facility ?? "",
      facilityName:
        lien.facilityName || facilityName(lien.facilityId ?? "") || "",
      serviceDate: lien.initialServiceDate,
      purchaseDate: lien.purchaseDate,
      originalAmount,
      reductionAmount,
      reductionDate,
      purchaseAmount: lien.purchaseAmount ?? 0,
      isServicing: lien.isServicing ?? false,
      paymentAmount,
      balance: totalBalance > 0 ? totalBalance : 0,
      closedAtUtc: lien.closedAtUtc ?? null,
    };
  });
}

async function fetchCaseLiens(
  caseId: string,
  query: LiensQuery,
  queryClient: QueryClient,
): Promise<{ items: CaseLienRow[]; pagination: PaginationMeta }> {
  const page = await fetchLienPage(caseId, query);
  const items = await enrichLiens(caseId, page.items, queryClient);
  return { items, pagination: page.pagination };
}

async function fetchAllCaseLiens(
  caseId: string,
  query: LiensQuery,
  queryClient: QueryClient,
): Promise<{ items: CaseLienRow[]; pagination: PaginationMeta }> {
  const rawItems = await fetchAllLienPages(caseId, query);
  const items = await enrichLiens(caseId, rawItems, queryClient);
  return {
    items,
    pagination: {
      page: 1,
      pageSize: items.length,
      totalCount: items.length,
      totalPages: 1,
    },
  };
}

export function useCaseLiens(
  caseId: string,
  query: LiensQuery,
  activeTab: string = "liens",
) {
  const queryClient = useQueryClient();

  // Lien tab — server-paginated per the caller-supplied page/pageSize.
  const pagedQuery = useQuery({
    queryKey: ["case-liens", caseId, query],
    queryFn: () => fetchCaseLiens(caseId, query, queryClient),
    enabled: activeTab === "liens",
  });

  // Servicing tab — needs every lien on the case (split into open/closed and
  // paginated client-side), so it walks every page of the capped list endpoint.
  // See TODO on fetchAllLienPages.
  const allQuery = useQuery({
    queryKey: ["case-liens-all", caseId],
    queryFn: () => fetchAllCaseLiens(caseId, query, queryClient),
    enabled: activeTab === "all-liens",
  });

  return activeTab == "liens" ? pagedQuery : allQuery;
}

export function useCaseDetail(caseId: string) {
  return useQuery({
    queryKey: ["caseDetail", caseId],
    queryFn: () => casesService.getCase(caseId),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

export function useUpdateServicingDetails() {
  const queryClient = useQueryClient();

  // Example mutation function
  return useMutation({
    mutationFn: (updatedData: UpdateServicingDetailsRequestDto) =>
      servicingService.updateDetails(updatedData),

    onSuccess: (data, variables) => {
      // Invalidate and refetch the specific case detail
      queryClient.invalidateQueries({
        queryKey: ["caseDetail", variables.caseId],
      });
    },
  });
}

export function useCases(query: CasesQuery) {
  return useQuery({
    queryKey: ["cases", query],
    queryFn: () => casesService.getCases(query),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

export function useLiens(query: LiensQuery) {
  return useQuery({
    queryKey: ["liens", query],
    queryFn: () => liensService.getLiens(query),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

async function fetchProcedureCodes() {
  const codes = await lookupService.getMedicalProcedureCodes();
  const uniqueCodes = Array.from(
    new Map(
      codes.data.map((item) => [`${item.code}-${item.description}`, item]),
    ).values(),
  );
  const list = uniqueCodes.map((item, index) => ({
    key: item.code,
    value: item.code,
    label: item.description,
  }));
  return list ?? [];
}

export function useMedicareProcedureCodes() {
  return useQuery({
    queryKey: ["procedureCodes"],
    queryFn: () => fetchProcedureCodes(),
    refetchOnWindowFocus: false,
  });
}

async function findMedicareCost(id: string): Promise<string> {
  if (!id) return "";
  const costs = await lookupService.getMedicalProcedureCosts(id);
  return costs?.total?.toString() ?? "";
}
export function useMedicareCosts(id: string) {
  return useQuery({
    queryKey: ["medicareCosts", id],
    queryFn: () => findMedicareCost(id),
    refetchOnWindowFocus: false,
  });
}

export function useCreateMedicalCode() {
  const queryClient = useQueryClient();

  // 1. Get your codes and lookup function here (from the previous step)
  const { data: medicalCodes } = useMedicareProcedureCodes();

  const findCodeByDescription = (description: string) => {
    if (!medicalCodes) return "";
    return medicalCodes.find((c) => c.value === description)?.key ?? "";
  };

  // 2. Define the execution logic inside the hook
  const createMedicalCodeLiens = async (
    payload: CreateMedicalCodeLiensDto,
    isEditing: boolean,
    lienId: string,
  ) => {
    const selectedCode =
      payload.code ||
      (typeof (payload as any).procedureCode === "string"
        ? (payload as any).procedureCode
        : "");

    const request: CreateMedicalCodeLiensDto = {
      id: payload.id,
      liensId: lienId,
      code: findCodeByDescription(selectedCode),
      medicareCost: parseFloat(payload.medicareCost).toFixed(2),
      billingAmount: parseFloat(payload.billingAmount).toFixed(2),
      purchaseAmount: parseFloat(payload.purchaseAmount).toFixed(2),
      payee: payload.payee,
      outboundCheckNumber: payload.outboundCheckNumber,
    };

    const res = isEditing
      ? await casesService.updateMedicalCodeLiens(request)
      : await casesService.createMedicalCodeLiens(request);

    return res;
  };

  // 3. Return the mutation using TanStack Query
  return useMutation({
    mutationFn: ({
      payload,
      isEditing,
      lienId,
    }: {
      payload: CreateMedicalCodeLiensDto;
      isEditing: boolean;
      lienId: string;
    }) => createMedicalCodeLiens(payload, isEditing, lienId),

    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["cases"],
      });
    },
  });
}

export function useCreateCase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (payload: CreateCaseRequestDto) =>
      casesService.createCase(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["cases"],
      });
    },
  });
}
export function useDeleteCase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => casesService.deleteCase(id),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["cases"],
      });
    },
  });
}

export function useDeleteLien(caseId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (liensId: string) => casesService.deleteLien(liensId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["case-liens", caseId] });
      queryClient.invalidateQueries({ queryKey: ["case-liens-all", caseId] });
    },
  });
}
