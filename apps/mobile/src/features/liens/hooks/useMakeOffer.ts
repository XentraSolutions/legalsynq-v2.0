import { useMutation, useQueryClient } from '@tanstack/react-query';

import { MockStore } from '@/features/mockStore';

export interface MakeOfferInput {
  lienId: string;
  offerAmount: number;
  notes?: string;
}

export function useMakeOffer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: MakeOfferInput) =>
      MockStore.makeOffer(input.lienId, input.offerAmount, input.notes),
    onSuccess: async (_, input) => {
      await queryClient.invalidateQueries({ queryKey: ['feature-liens'] });
      await queryClient.invalidateQueries({ queryKey: ['offers'] });
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      await queryClient.invalidateQueries({ queryKey: ['feature-liens', 'detail', input.lienId] });
    },
  });
}
