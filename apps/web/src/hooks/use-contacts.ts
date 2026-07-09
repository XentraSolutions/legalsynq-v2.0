"use client";

import { useMutation, useQuery, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { contactsService, type ContactsQuery } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import type { BatchReassignCasesRequestDto } from "@/lib/cases/cases.types";

export const CONTACTS_QUERY_KEY = (query: ContactsQuery) =>
  ["contacts", query] as const;

export const CONTACT_TYPES_QUERY_KEY = ["contact-types"] as const;

export function useContacts(query: ContactsQuery) {
  return useQuery({
    queryKey: CONTACTS_QUERY_KEY(query),
    queryFn: () => contactsService.getContacts(query),
    staleTime: 0,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
  });
}

export function useContactTypes() {
  return useQuery({
    queryKey: CONTACT_TYPES_QUERY_KEY,
    queryFn: () => lookupService.getContactTypes(),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

export function useDeleteContact() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => contactsService.deleteContact(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["contacts"] });
    },
  });
}

export function useBatchReassignContact() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BatchReassignCasesRequestDto) =>
      contactsService.batchReassignCases(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["contacts"] });
    },
  });
}
