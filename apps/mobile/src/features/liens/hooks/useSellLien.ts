import { useMutation, useQueryClient } from '@tanstack/react-query';

import { MockStore } from '@/features/mockStore';
import type { LienCaseType } from '@/shared/api/endpoints/Liens';

export interface SellLienInput {
  patientName: string;
  caseType: LienCaseType;
  jurisdiction: string;
  lienAmount: number;
  askingPrice: number;
}

export function useSellLien() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: SellLienInput) => MockStore.sellLien(input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['feature-liens'] });
      await queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    },
  });
}
