"use client";

import { useMutation } from "@tanstack/react-query";
import { liensService } from "@/lib/selling";
import type {
  CreateCaseDraftRequest,
  AttachPlaintiffRequest,
} from "@/lib/selling";

export function useCreateCaseDraft() {
  return useMutation({
    mutationFn: (request: CreateCaseDraftRequest) =>
      liensService.createCaseDraft(request),
  });
}

export function useAttachPlaintiff() {
  return useMutation({
    mutationFn: ({
      draftId,
      request,
    }: {
      draftId: string;
      request: AttachPlaintiffRequest;
    }) => liensService.attachPlaintiff(draftId, request),
  });
}
