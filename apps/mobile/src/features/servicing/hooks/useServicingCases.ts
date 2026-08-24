import { useMemo } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';

import { useCases } from '@/features/cases/hooks';
import {
  exportServicingCases,
  filterServicingCases,
  loadServicingCases,
} from '@/features/servicing/services/servicingCaseService';
import type { ServicingCaseListItem } from '@/features/servicing/types/types';

export const servicingCaseKeys = {
  all: ['servicing-cases'] as const,
  list: (caseDataUpdatedAt: number) => [...servicingCaseKeys.all, caseDataUpdatedAt] as const,
};

export function useServicingCases(search = '') {
  const casesQuery = useCases();
  const servicingQuery = useQuery({
    queryKey: servicingCaseKeys.list(casesQuery.dataUpdatedAt),
    queryFn: () => loadServicingCases(casesQuery.cases),
    enabled: casesQuery.isSuccess,
  });
  const allCases = servicingQuery.data ?? [];
  const cases = useMemo(() => filterServicingCases(allCases, search), [allCases, search]);

  return {
    ...servicingQuery,
    cases,
    error: casesQuery.error ?? servicingQuery.error,
    isError: casesQuery.isError || servicingQuery.isError,
    isLoading: casesQuery.isLoading || servicingQuery.isLoading,
    isRefetching: casesQuery.isRefetching || servicingQuery.isRefetching,
    totalCount: allCases.length,
    refetchAll: async () => {
      await casesQuery.refetch();
      await servicingQuery.refetch();
    },
  };
}

export function useExportServicingCases() {
  return useMutation({
    mutationFn: (cases: ServicingCaseListItem[]) => exportServicingCases(cases),
  });
}
