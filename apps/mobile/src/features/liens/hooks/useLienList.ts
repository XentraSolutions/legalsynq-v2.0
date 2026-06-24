import { useMemo } from 'react';
import { useInfiniteQuery } from '@tanstack/react-query';

import { MockStore } from '@/features/mockStore';
import type { LienFilter } from '@/features/liens/types/types';

const PAGE_SIZE = 20;

export const lienListKeys = {
  all: ['feature-liens'] as const,
  list: (filterId: string, search: string) => [...lienListKeys.all, 'list', filterId, search] as const,
};

export function useLienList(filter: LienFilter, search: string) {
  const query = useInfiniteQuery({
    queryKey: lienListKeys.list(filter.id, search),
    queryFn: async ({ pageParam = 0 }) => {
      const allLiens = await MockStore.listLiens();
      const normalizedSearch = search.trim().toLowerCase();
      const filtered = allLiens.filter((lien) => {
        if (filter.caseType && lien.caseType !== filter.caseType) {
          return false;
        }
        if (filter.status && lien.status !== filter.status) {
          return false;
        }
        if (filter.maxAmount && lien.askingPrice && lien.askingPrice > filter.maxAmount) {
          return false;
        }
        if (filter.minAmount && lien.askingPrice && lien.askingPrice < filter.minAmount) {
          return false;
        }
        if (!normalizedSearch) {
          return true;
        }

        return [lien.patientName, lien.caseReference, lien.jurisdiction, lien.sellerOrgName]
          .join(' ')
          .toLowerCase()
          .includes(normalizedSearch);
      });

      const start = pageParam * PAGE_SIZE;
      return {
        items: filtered.slice(start, start + PAGE_SIZE),
        page: pageParam,
        totalCount: filtered.length,
      };
    },
    initialPageParam: 0,
    getNextPageParam: (lastPage) => {
      const nextPage = lastPage.page + 1;
      return nextPage * PAGE_SIZE < lastPage.totalCount ? nextPage : undefined;
    },
  });

  const liens = useMemo(() => query.data?.pages.flatMap((page) => page.items) ?? [], [query.data]);
  const totalCount = query.data?.pages[0]?.totalCount ?? 0;

  return {
    ...query,
    liens,
    totalCount,
  };
}
