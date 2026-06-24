import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { MockStore } from '@/features/mockStore';

export const caseFeatureKeys = {
  all: ['cases'] as const,
  list: (search: string) => [...caseFeatureKeys.all, 'list', search] as const,
  detail: (id: string) => [...caseFeatureKeys.all, 'detail', id] as const,
  notes: (id: string) => [...caseFeatureKeys.all, 'notes', id] as const,
};

export function useCases(search: string) {
  const query = useQuery({
    queryKey: caseFeatureKeys.list(search),
    queryFn: MockStore.listCases,
  });

  const cases = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    if (!normalizedSearch) {
      return query.data ?? [];
    }

    return (query.data ?? []).filter((caseItem) =>
      [caseItem.patientName, caseItem.caseReference, caseItem.jurisdiction]
        .join(' ')
        .toLowerCase()
        .includes(normalizedSearch)
    );
  }, [query.data, search]);

  return {
    ...query,
    cases,
  };
}

export function useCaseDetail(caseId: string) {
  return useQuery({
    queryKey: caseFeatureKeys.detail(caseId),
    queryFn: () => MockStore.getCase(caseId),
  });
}

export function useCaseNotes(caseId: string) {
  return useQuery({
    queryKey: caseFeatureKeys.notes(caseId),
    queryFn: () => MockStore.getCaseNotes(caseId),
  });
}

export function useAddCaseNote(caseId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (content: string) => MockStore.addCaseNote(caseId, content),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: caseFeatureKeys.notes(caseId) });
    },
  });
}
