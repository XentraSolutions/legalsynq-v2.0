"use client";

import { useMutation, useQuery, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { contactsService } from "@/lib/contacts";
import type { ContactCaseSummary } from "@/lib/contacts/contacts.types";
import type { ContactCaseLookupParams } from "@/lib/cases/cases.types";

export const CONTACT_CASES_QUERY_KEY = (
  contactId: string,
  contactType: string,
  params: ContactCaseLookupParams,
) => ["contact-cases", contactId, contactType, params] as const;

export function useContactCases(
  contactId: string,
  contactType: string,
  params: ContactCaseLookupParams,
  enabled: boolean,
) {
  return useQuery({
    queryKey: CONTACT_CASES_QUERY_KEY(contactId, contactType, params),
    queryFn: () => contactsService.getCasesByContact(contactId, contactType, params),
    enabled,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
  });
}

export function useReassignContactCase() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: {
      contactType: string;
      item: ContactCaseSummary;
      newPrimaryId: string;
      newSecondaryId?: string;
    }) => contactsService.reassignCase(params),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["contact-cases"] });
    },
  });
}
