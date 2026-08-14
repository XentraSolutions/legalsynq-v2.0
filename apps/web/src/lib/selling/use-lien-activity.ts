import { useQuery } from "@tanstack/react-query";
import { liensService } from "./selling-liens.service";

export function lienActivityQueryKey(lienId?: string) {
  return ["lien-activity", lienId] as const;
}

export function useLienActivity(lienId?: string) {
  return useQuery({
    queryKey: lienActivityQueryKey(lienId),
    queryFn: () => liensService.getLienActivity(lienId as string),
    enabled: !!lienId,
    staleTime: 0,
  });
}
