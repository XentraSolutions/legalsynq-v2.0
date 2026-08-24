"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { batchService } from "@/lib/batch/batch.service";
import {
  BatchListItem,
  PaginationMeta,
  TemplateItem,
} from "@/lib/batch/batch.types";
import { PaginatedResultWithItems } from "@/lib/lookup/lookup.types";

export function useBatchList(query: PaginationMeta) {
  return useQuery({
    queryKey: ["batch-upload", query],
    queryFn: () => batchService.getBatchList(query),
    staleTime: 30_000,
  });
}

export function useCreateBatch() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: batchService.createBatch,
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["batch-upload"],
      });
    },
  });
}

export function useUpdateBatch() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: batchService.update,
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["batch-upload"],
      });
    },
  });
}
