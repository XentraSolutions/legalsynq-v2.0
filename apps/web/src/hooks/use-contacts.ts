"use client";

import {
  useMutation,
  useQuery,
  useQueryClient,
  useInfiniteQuery,
  keepPreviousData,
} from "@tanstack/react-query";
import { contactsService, type ContactsQuery } from "@/lib/contacts";
import { lookupService } from "@/lib/lookup";
import type { BatchReassignCasesRequestDto } from "@/lib/cases/cases.types";

export const CONTACTS_QUERY_KEY = (query: ContactsQuery) =>
  ["contacts", query] as const;

export const CONTACT_QUERY_KEY = (id: string) => ["contact", id] as const;

/**
 * Resolves a single contact by id — used to seed a dropdown's display label
 * when it's initialized with a value (e.g. from saved form data) that isn't
 * necessarily among the currently loaded list options.
 */
export function useContact(id: string | null | undefined) {
  return useQuery({
    queryKey: CONTACT_QUERY_KEY(id ?? ""),
    queryFn: () => contactsService.getContact(id as string),
    enabled: Boolean(id),
    staleTime: 30_000,
    retry: false,
  });
}

export const MEDICAL_FACILITY_BY_FACILITY_ID_QUERY_KEY = (facilityId: string) =>
  ["contact-by-facility-id", facilityId] as const;

/**
 * Medical facility liens (the legacy `get-facility`/`update-facility` API)
 * store a facility's own `facilityId` field rather than its contact id —
 * pre-dating the unified Contacts system, where a MedicalFacility contact's
 * `id` normally doubles as the value used elsewhere. Used as a fallback to
 * resolve a display label when a direct by-id lookup finds no match.
 */
export function useMedicalFacilityByFacilityId(facilityId: string | null | undefined) {
  return useQuery({
    queryKey: MEDICAL_FACILITY_BY_FACILITY_ID_QUERY_KEY(facilityId ?? ""),
    queryFn: async () => {
      const { items } = await contactsService.getContacts({
        ContactType: "MedicalFacility",
        // Explicit "" restricts the match to the main facility record,
        // excluding its FacilityContactPerson sub-contacts.
        ContactSubtype: "",
        FacilityId: facilityId as string,
        page: 1,
        pageSize: 1,
      });
      return items[0] ?? null;
    },
    enabled: Boolean(facilityId),
    staleTime: 30_000,
    retry: false,
  });
}

const CONTACTS_PAGE_SIZE = 25;

export const INFINITE_CONTACTS_QUERY_KEY = (query: ContactsQuery) =>
  ["contacts-infinite", query] as const;

/**
 * Lazy, scroll-paginated contacts query — only fetches the next page once
 * the caller actually asks for it (e.g. BaseSelect's scroll-sentinel via
 * `fetchNextPage`), unlike the eager background-preload-all-pages pattern
 * in use-filter-options.ts. Pairs with BaseSelect's `loadingMode="infinite"`.
 */
export function useInfiniteContacts(query: ContactsQuery, options?: { enabled?: boolean }) {
  return useInfiniteQuery({
    queryKey: INFINITE_CONTACTS_QUERY_KEY(query),
    queryFn: ({ pageParam }) =>
      contactsService.getContacts({ ...query, page: pageParam, pageSize: CONTACTS_PAGE_SIZE }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.pagination.page < lastPage.pagination.totalPages
        ? lastPage.pagination.page + 1
        : undefined,
    staleTime: 0,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
    // Infinite queries refetch every already-cached page (not just page 1)
    // on remount by default — with staleTime: 0 that means reopening this
    // select after scrolling through a few pages last time would eagerly
    // re-fetch all of them at once. Disabling refetchOnMount keeps a
    // reopen cheap (page 1 from cache); invalidateQueries (e.g. after
    // creating a contact) still refetches regardless of this setting.
    refetchOnMount: false,
    enabled: options?.enabled,
  });
}

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
