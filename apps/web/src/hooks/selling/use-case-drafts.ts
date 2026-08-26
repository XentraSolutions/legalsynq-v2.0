"use client";

import { useQuery, useMutation } from "@tanstack/react-query";
import { liensService } from "@/lib/selling";
import type {
  CaseDraftRequest,
  FinalizeCaseDraftRequest,
  UpdateCaseRequest,
  UpdateCasePlaintiffRequest,
} from "@/lib/selling";

export function useCreateCaseDraft() {
  return useMutation({
    mutationFn: (request: CaseDraftRequest) =>
      liensService.createCaseDraft(request),
  });
}

export function useUpdateCaseDraft() {
  return useMutation({
    mutationFn: ({
      draftId,
      request,
    }: {
      draftId: string;
      request: CaseDraftRequest;
    }) => liensService.updateCaseDraft(draftId, request),
  });
}

export const CASE_DRAFT_QUERY_KEY = (draftId: string) =>
  ["selling-case-draft", draftId] as const;

export function useCaseDraft(
  draftId: string | null | undefined,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(draftId);
  return useQuery({
    queryKey: CASE_DRAFT_QUERY_KEY(draftId ?? ""),
    queryFn: () => liensService.getCaseDraftById(draftId as string),
    enabled,
    staleTime: 30_000,
    retry: false,
  });
}

export function useFinalizeCaseDraft() {
  return useMutation({
    mutationFn: ({
      draftId,
      request,
    }: {
      draftId: string;
      request: FinalizeCaseDraftRequest;
    }) => liensService.finalizeCaseDraft(draftId, request),
  });
}

export const SELLING_CASE_QUERY_KEY = (caseId: string) =>
  ["selling-case", caseId] as const;

// Named useSellingCaseDetail (not useCase) to avoid colliding with the
// non-selling Cases module's useCase() in use-cases-search.ts.
export function useSellingCaseDetail(
  caseId: string | null | undefined,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(caseId);
  return useQuery({
    queryKey: SELLING_CASE_QUERY_KEY(caseId ?? ""),
    queryFn: () => liensService.getCaseById(caseId as string),
    enabled,
    staleTime: 30_000,
    retry: false,
  });
}

// Updates case info only. The plaintiff is always updated separately via
// useUpdateCasePlaintiff — see UpdateCaseRequest in liens.types.ts.
export function useUpdateCase() {
  return useMutation({
    mutationFn: ({
      caseId,
      request,
    }: {
      caseId: string;
      request: UpdateCaseRequest;
    }) => liensService.updateCase(caseId, request),
  });
}

export function useUpdateCasePlaintiff() {
  return useMutation({
    mutationFn: ({
      caseId,
      request,
    }: {
      caseId: string;
      request: UpdateCasePlaintiffRequest;
    }) => liensService.updateCasePlaintiff(caseId, request),
  });
}
