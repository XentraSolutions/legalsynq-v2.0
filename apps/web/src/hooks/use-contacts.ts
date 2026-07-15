"use client";

import { useMutation, useQuery, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { contactsService, type ContactsQuery } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import type { BatchReassignCasesRequestDto } from "@/lib/cases/cases.types";

export const CONTACTS_QUERY_KEY = (query: ContactsQuery) =>
  ["contacts", query] as const;

export const CONTACT_TYPES_QUERY_KEY = ["contact-types"] as const;

export function useContacts(query: ContactsQuery, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: CONTACTS_QUERY_KEY(query),
    queryFn: () => contactsService.getContacts(query),
    staleTime: 0,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
    enabled: options?.enabled,
  });
}

// TODO: Temporary fix — hardcoded allowlist to only surface known contact
// types. Remove once the ContactType lookup data/API supports this properly.
export const KNOWN_CONTACT_TYPE_CODES = [
  "LawFirm",
  "MedicalFacility",
  "Provider",
  "FundingCompany",
  "Lead",
];

export function useContactTypes(options?: { knownOnly?: boolean }) {
  const knownOnly = options?.knownOnly ?? false;
  return useQuery({
    queryKey: [...CONTACT_TYPES_QUERY_KEY, { knownOnly }] as const,
    queryFn: () => lookupService.getContactTypes(),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
    select: (data) => ({
      items: knownOnly
        ? data.items.filter((t) => KNOWN_CONTACT_TYPE_CODES.includes(t.code))
        : data.items,
    }),
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
