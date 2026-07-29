import { useQuery } from '@tanstack/react-query';

import { MockStore } from '@/features/mockStore';

export const lienDetailKeys = {
  detail: (id: string) => ['feature-liens', 'detail', id] as const,
};

export function useLienDetail(lienId: string) {
  return useQuery({
    queryKey: lienDetailKeys.detail(lienId),
    queryFn: () => MockStore.getLien(lienId),
  });
}
