import { useQuery } from '@tanstack/react-query';

import { ApplicationsApi } from '@/shared/api/endpoints/Applications';

export const applicationKeys = {
  all: ['applications'] as const,
  detail: (applicationId: string) => [...applicationKeys.all, 'detail', applicationId] as const,
};

export function useApplicationDetail(applicationId: string) {
  return useQuery({
    queryKey: applicationKeys.detail(applicationId),
    queryFn: () => ApplicationsApi.get(applicationId),
  });
}
