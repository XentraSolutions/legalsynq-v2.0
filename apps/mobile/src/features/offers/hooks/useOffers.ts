import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { DEMO_USER } from '@/features/mockData';
import { MockStore } from '@/features/mockStore';
import type { OfferDirection } from '@/features/offers/types/types';

export const offerFeatureKeys = {
  all: ['offers'] as const,
  list: (direction: OfferDirection) => [...offerFeatureKeys.all, 'list', direction] as const,
  detail: (id: string) => [...offerFeatureKeys.all, 'detail', id] as const,
};

export function useOffers(direction: OfferDirection) {
  const query = useQuery({
    queryKey: offerFeatureKeys.list(direction),
    queryFn: MockStore.listOffers,
  });

  const offers = useMemo(
    () =>
      (query.data ?? []).filter((offer) =>
        direction === 'sent' ? offer.buyerId === DEMO_USER.id : offer.buyerId !== DEMO_USER.id
      ),
    [direction, query.data]
  );

  return {
    ...query,
    offers,
  };
}

export function useOfferDetail(offerId: string) {
  return useQuery({
    queryKey: offerFeatureKeys.detail(offerId),
    queryFn: () => MockStore.getOffer(offerId),
  });
}

export function useOfferActions() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ offerId, status }: { offerId: string; status: 'ACCEPTED' | 'DECLINED' }) =>
      MockStore.updateOffer(offerId, { status }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: offerFeatureKeys.all });
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });
}
