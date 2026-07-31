import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useAtomValue } from 'jotai';

import { buildCaseFilterOptions, filterCases, mapCaseReportRow } from '@/features/cases/mappers/caseMappers';
import { CaseExportService } from '@/features/cases/services/caseExportService';
import type { CaseFilters } from '@/features/cases/types/types';
import { EMPTY_CASE_FILTERS } from '@/features/cases/types/types';
import {
  CasesApi,
  LegacyCasesAdapter,
  LegacyCasesApi,
} from '@/shared/api/endpoints/Cases';
import { ContactsApi } from '@/shared/api/endpoints/Contacts';
import { LookupsApi } from '@/shared/api/endpoints/Lookups';
import type {
  CaseDetailsUpdateRequest,
  CaseExportFilter,
  CreateCaseRequest,
  PersonalCaseUpdateRequest,
  PrimaryCaseUpdateRequest,
} from '@/shared/api/endpoints/Cases';
import { useAuth } from '@/shared/hooks';
import { apiModeAtom } from '@/shared/state/atoms/apiModeAtom';

export const caseFeatureKeys = {
  all: ['cases'] as const,
  list: (mode: string) => [...caseFeatureKeys.all, 'list', mode] as const,
  detail: (id: string) => [...caseFeatureKeys.all, 'detail', id] as const,
  notes: (id: string) => [...caseFeatureKeys.all, 'notes', id] as const,
  updates: (id: string) => [...caseFeatureKeys.all, 'updates', id] as const,
  payoffQuote: (id: string) => [...caseFeatureKeys.all, 'payoff-quote', id] as const,
  trackingOptions: () => [...caseFeatureKeys.all, 'tracking-options'] as const,
};

export function useCaseTrackingOptions() {
  return useQuery({
    queryKey: caseFeatureKeys.trackingOptions(),
    queryFn: async () => {
      const [medicalStatuses, caseTypes, states, leads] = await Promise.allSettled([
        LookupsApi.getByCategory('MedicalStatus'),
        LookupsApi.getByCategory('AccidentType'),
        LookupsApi.getByCategory('State'),
        ContactsApi.listByType('Lead'),
      ]);

      return {
        medicalStatuses: medicalStatuses.status === 'fulfilled' ? medicalStatuses.value : [],
        caseTypes: caseTypes.status === 'fulfilled' ? caseTypes.value : [],
        states: states.status === 'fulfilled' ? states.value : [],
        leads: leads.status === 'fulfilled' ? leads.value : [],
      };
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useCases(search = '', filters: CaseFilters = EMPTY_CASE_FILTERS) {
  const mode = useAtomValue(apiModeAtom);
  const query = useQuery({
    queryKey: caseFeatureKeys.list(mode),
    queryFn: async () => {
      const rows =
        mode === 'legacy'
          ? (
              await LegacyCasesApi.getDashboardTotalCaseReportV3({ page: 1, limit: 1000000 })
                .then(LegacyCasesAdapter.toTotalCaseReportPage)
            ).items
          : await CasesApi.getDashboardTotalCaseReport();
      return rows.map(mapCaseReportRow);
    },
  });

  const cases = useMemo(() => {
    return filterCases(query.data ?? [], search, filters);
  }, [filters, query.data, search]);

  const filterOptions = useMemo(() => buildCaseFilterOptions(query.data ?? []), [query.data]);

  return {
    ...query,
    cases,
    filterOptions,
    totalCount: query.data?.length ?? 0,
  };
}

export function useCaseDetail(caseId: string) {
  const mode = useAtomValue(apiModeAtom);
  return useQuery({
    queryKey: caseFeatureKeys.detail(caseId),
    queryFn: () => {
      if (mode === 'legacy') {
        throw new Error('Case details are not available in Legacy API mode.');
      }
      return CasesApi.getCase(caseId);
    },
  });
}

export function usePayoffQuote(caseId: string) {
  return useQuery({
    queryKey: caseFeatureKeys.payoffQuote(caseId),
    queryFn: () => CasesApi.getPayoffQuote(caseId),
    retry: false,
  });
}

export function useMergeCase(caseId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (mergedCaseId: string) => CasesApi.mergeCase(caseId, mergedCaseId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: caseFeatureKeys.detail(caseId) }),
        queryClient.invalidateQueries({ queryKey: caseFeatureKeys.updates(caseId) }),
        queryClient.invalidateQueries({ queryKey: caseFeatureKeys.all }),
      ]);
    },
  });
}

export function useDeleteCase(caseId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => CasesApi.deleteCase(caseId),
    onSuccess: async () => {
      queryClient.removeQueries({ queryKey: caseFeatureKeys.detail(caseId) });
      queryClient.removeQueries({ queryKey: caseFeatureKeys.notes(caseId) });
      queryClient.removeQueries({ queryKey: caseFeatureKeys.updates(caseId) });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: [...caseFeatureKeys.all, 'list'] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ]);
    },
  });
}

export function useCaseUpdates(caseId: string) {
  return useQuery({
    queryKey: caseFeatureKeys.updates(caseId),
    queryFn: () => CasesApi.getCaseUpdates(caseId),
  });
}

function useRefreshCase(caseId: string) {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: caseFeatureKeys.detail(caseId) }),
      queryClient.invalidateQueries({ queryKey: caseFeatureKeys.updates(caseId) }),
      queryClient.invalidateQueries({ queryKey: caseFeatureKeys.all }),
    ]);
  };
}

export function useUpdatePersonalInfo(caseId: string) {
  const refreshCase = useRefreshCase(caseId);
  return useMutation({
    mutationFn: (input: Omit<PersonalCaseUpdateRequest, 'caseId'>) =>
      CasesApi.updatePersonalInfo({ ...input, caseId }),
    onSuccess: refreshCase,
  });
}

export function useUpdateCaseDetails(caseId: string) {
  const refreshCase = useRefreshCase(caseId);
  return useMutation({
    mutationFn: async ({
      primary,
      details,
    }: {
      primary: Omit<PrimaryCaseUpdateRequest, 'caseId'>;
      details: Omit<CaseDetailsUpdateRequest, 'caseId'>;
    }) => {
      await CasesApi.updatePrimaryInfo({ ...primary, caseId });
      await CasesApi.updateCaseDetails({ ...details, caseId });
    },
    onSuccess: refreshCase,
  });
}

export function useCaseNotes(caseId: string) {
  const mode = useAtomValue(apiModeAtom);
  return useQuery({
    queryKey: caseFeatureKeys.notes(caseId),
    queryFn: () => {
      if (mode === 'legacy') return [];
      return CasesApi.getCaseNotes(caseId);
    },
  });
}

export function useAddCaseNote(caseId: string) {
  const queryClient = useQueryClient();
  const { user } = useAuth();

  return useMutation({
    mutationFn: (content: string) =>
      CasesApi.addCaseNote(caseId, {
        content,
        category: 'general',
        createdByName: user ? `${user.firstName} ${user.lastName}`.trim() : 'Mobile user',
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: caseFeatureKeys.notes(caseId) });
    },
  });
}

export function useCreateCase() {
  const queryClient = useQueryClient();
  const mode = useAtomValue(apiModeAtom);

  return useMutation({
    mutationFn: (input: CreateCaseRequest) => {
      if (mode === 'legacy') {
        throw new Error('Case creation is not available in Legacy API mode.');
      }
      return CasesApi.createCase(input);
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: caseFeatureKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ]);
    },
  });
}

export function useExportCases() {
  const mode = useAtomValue(apiModeAtom);
  return useMutation({
    mutationFn: async (filters: CaseExportFilter) => {
      if (mode === 'legacy') {
        throw new Error('Case export is not available in Legacy API mode.');
      }
      const file = await CasesApi.exportCases(filters);
      await CaseExportService.share(file);
    },
  });
}
