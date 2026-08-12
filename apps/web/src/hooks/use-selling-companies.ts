"use client";

import { useCallback, useEffect, useMemo } from "react";
import {
  keepPreviousData,
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import { companiesApi } from "@/lib/selling/companies.api";
import type { BaseSelectOption } from "@/components/ui/base-select";
import type { InfiniteOptions } from "@/hooks/use-filter-options";
import type {
  CompaniesPaginatedResult,
  CompaniesQuery,
  CreateCompanyRequest,
  UpdateCompanyRequest,
  CreateContactPersonRequest,
  UpdateContactPersonRequest,
  CreateContactPersonTypeRequest,
  CompaniesExportQuery,
  ContactPersonsExportQuery,
  ContactsExportQuery,
  ReassignCompanyRequest,
  ReassignContactPersonRequest,
} from "@/lib/selling/companies.types";

function toOptions(items: { id: string; name: string }[]): BaseSelectOption[] {
  return items.map((item) => ({ value: item.id, label: item.name }));
}

function toContactOptions(
  items: { id: string; displayName: string }[],
): BaseSelectOption[] {
  return items.map((item) => ({ value: item.id, label: item.displayName }));
}

// ── Lookups ──────────────────────────────────────────────────────────────────

export const COMPANY_TYPES_QUERY_KEY = ["selling-company-types"] as const;

export function useCompanyTypes(options?: { enabled?: boolean }) {
  const query = useQuery({
    queryKey: COMPANY_TYPES_QUERY_KEY,
    queryFn: () => companiesApi.companyTypes().then(({ data }) => data.items),
    staleTime: 30_000,
    enabled: options?.enabled,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}

export const CONTACT_PERSON_TYPES_QUERY_KEY = (companyTypeId: string) =>
  ["selling-contact-person-types", companyTypeId] as const;

export function useContactPersonTypes(
  companyTypeId: string | null | undefined,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(companyTypeId);
  const query = useQuery({
    queryKey: CONTACT_PERSON_TYPES_QUERY_KEY(companyTypeId ?? ""),
    queryFn: () =>
      companiesApi
        .contactPersonTypes(companyTypeId as string)
        .then(({ data }) => data.items),
    enabled,
    staleTime: 30_000,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}

export function useCreateContactPersonType() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateContactPersonTypeRequest) =>
      companiesApi.createContactPersonType(request).then(({ data }) => data),
    onSuccess: (_data, { companyTypeId }) => {
      queryClient.invalidateQueries({ queryKey: CONTACT_PERSON_TYPES_QUERY_KEY(companyTypeId) });
    },
  });
}

// ── Companies ────────────────────────────────────────────────────────────────

export const COMPANIES_QUERY_KEY = (query: CompaniesQuery = {}) =>
  ["selling-companies", query] as const;

export function useCompanies(query: CompaniesQuery = {}, options?: { enabled?: boolean }) {
  const q = useQuery({
    queryKey: COMPANIES_QUERY_KEY(query),
    queryFn: () => companiesApi.listCompanies(query).then(({ data }) => data),
    staleTime: 30_000,
    enabled: options?.enabled,
  });
  return {
    ...q,
    options: useMemo(() => toOptions(q.data?.items ?? []), [q.data]),
  };
}

const COMPANIES_INFINITE_PAGE_SIZE = 20;

/**
 * Scroll-paginated + debounced-server-search companies list, for pickers
 * (e.g. reassign target) where eagerly loading every page up front
 * (`useInfiniteCompanyOptions`) would be wasteful — this only fetches a page
 * at a time as the caller scrolls or types. Pairs with `BaseSelect`'s
 * `loadingMode="infinite"`, same shape as `useInfiniteContacts`.
 */
export function useInfiniteCompanies(
  query: CompaniesQuery,
  options?: { enabled?: boolean },
) {
  return useInfiniteQuery({
    queryKey: ["selling-companies-infinite", query],
    queryFn: ({ pageParam }) =>
      companiesApi
        .listCompanies({ ...query, page: pageParam, pageSize: COMPANIES_INFINITE_PAGE_SIZE })
        .then(({ data }) => data),
    initialPageParam: 1,
    getNextPageParam: nextCompanyOptionsPageParam,
    staleTime: 0,
    placeholderData: keepPreviousData,
    refetchOnWindowFocus: false,
    refetchOnMount: false,
    enabled: options?.enabled,
  });
}

export const COMPANY_QUERY_KEY = (id: string) => ["selling-company", id] as const;

export function useCompany(id: string | null | undefined, options?: { enabled?: boolean }) {
  const enabled = (options?.enabled ?? true) && Boolean(id);
  return useQuery({
    queryKey: COMPANY_QUERY_KEY(id ?? ""),
    queryFn: () => companiesApi.getCompany(id as string).then(({ data }) => data),
    enabled,
  });
}

const COMPANY_OPTIONS_PAGE_SIZE = 100;

function mapCompanyPageToOptions(page: CompaniesPaginatedResult): BaseSelectOption[] {
  return page.items.map((c) => ({ value: c.id, label: c.name }));
}

function nextCompanyOptionsPageParam(last: CompaniesPaginatedResult): number | undefined {
  const totalPages = Math.ceil(last.totalCount / last.pageSize) || 1;
  return last.page < totalPages ? last.page + 1 : undefined;
}

/**
 * Background-loaded `{value,label}` options for companies of a given type
 * (matched by `company-types` `code`, e.g. "FundingCompany") — the new
 * companies API, not the legacy contacts endpoints `useInfiniteContactOptions`
 * wraps. Same eager-background-pagination shape as that hook (see
 * use-filter-options.ts's module doc) so it's a drop-in `InfiniteOptions`
 * source for FilterSection/InfiniteFilterList.
 */
export function useInfiniteCompanyOptions(
  companyTypeCode: string,
  opts?: { enabled?: boolean },
): InfiniteOptions {
  const enabled = opts?.enabled ?? true;
  const companyTypesQuery = useCompanyTypes({ enabled });
  const companyType = companyTypesQuery.data?.find((t) => t.code === companyTypeCode);
  const queryEnabled = enabled && Boolean(companyType?.id);

  const query = useInfiniteQuery({
    queryKey: ["selling-company-options", companyTypeCode, companyType?.id],
    queryFn: ({ pageParam }) =>
      companiesApi
        .listCompanies({
          companyTypeId: companyType?.id,
          page: pageParam,
          pageSize: COMPANY_OPTIONS_PAGE_SIZE,
        })
        .then(({ data }) => data),
    initialPageParam: 1,
    getNextPageParam: nextCompanyOptionsPageParam,
    enabled: queryEnabled,
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });

  const { hasNextPage, isFetchingNextPage, isLoading, fetchNextPage } = query;
  useEffect(() => {
    if (queryEnabled && hasNextPage && !isFetchingNextPage && !isLoading) {
      fetchNextPage();
    }
  }, [queryEnabled, hasNextPage, isFetchingNextPage, isLoading, fetchNextPage]);

  const loadAll = useCallback(async () => {
    let result: typeof query = query;
    while (result.hasNextPage) {
      result = await result.fetchNextPage();
    }
    return (result.data?.pages ?? []).flatMap(mapCompanyPageToOptions);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query.data, query.hasNextPage, query.fetchNextPage]);

  return useMemo(
    () => ({
      options: (query.data?.pages ?? []).flatMap(mapCompanyPageToOptions),
      isLoading: query.isLoading,
      isFetchingMore: query.isFetchingNextPage,
      hasNextPage: query.hasNextPage,
      loadMore: query.fetchNextPage,
      allLoaded: !query.hasNextPage && !query.isLoading,
      loadAll,
    }),
    [query, loadAll],
  );
}

export function useCreateCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateCompanyRequest) =>
      companiesApi.createCompany(request).then(({ data }) => data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["selling-companies"] });
    },
  });
}

export function useUpdateCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateCompanyRequest }) =>
      companiesApi.updateCompany(id, request).then(({ data }) => data),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-companies"] });
      queryClient.invalidateQueries({ queryKey: COMPANY_QUERY_KEY(id) });
    },
  });
}

export function useDeactivateCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => companiesApi.deactivateCompany(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ["selling-companies"] });
      queryClient.invalidateQueries({ queryKey: COMPANY_QUERY_KEY(id) });
    },
  });
}

export function useReactivateCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => companiesApi.reactivateCompany(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ["selling-companies"] });
      queryClient.invalidateQueries({ queryKey: COMPANY_QUERY_KEY(id) });
    },
  });
}

export function useReassignCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: ReassignCompanyRequest }) =>
      companiesApi.reassignCompany(id, request).then(({ data }) => data),
    onSuccess: (_data, { id }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-companies"] });
      queryClient.invalidateQueries({ queryKey: COMPANY_QUERY_KEY(id) });
    },
  });
}

export function useExportCompanies() {
  return useMutation({
    mutationFn: (query: CompaniesExportQuery) => companiesApi.exportCompanies(query),
  });
}

export function useExportContacts() {
  return useMutation({
    mutationFn: (query: ContactsExportQuery) => companiesApi.exportContacts(query),
  });
}

// ── Contact persons ──────────────────────────────────────────────────────────

export const CONTACT_PERSONS_QUERY_KEY = (companyId: string, isActive?: boolean) =>
  ["selling-contact-persons", companyId, isActive ?? true] as const;

export function useContactPersons(
  companyId: string | null | undefined,
  isActive: boolean = true,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(companyId);
  const query = useQuery({
    queryKey: CONTACT_PERSONS_QUERY_KEY(companyId ?? "", isActive),
    queryFn: () =>
      companiesApi
        .listContactPersons(companyId as string, isActive)
        .then(({ data }) => data.items),
    enabled,
    staleTime: 30_000,
  });
  return {
    ...query,
    options: useMemo(() => toContactOptions(query.data ?? []), [query.data]),
  };
}

export const CONTACT_PERSON_QUERY_KEY = (companyId: string, contactId: string) =>
  ["selling-contact-person", companyId, contactId] as const;

export function useContactPerson(
  companyId: string | null | undefined,
  contactId: string | null | undefined,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(companyId) && Boolean(contactId);
  return useQuery({
    queryKey: CONTACT_PERSON_QUERY_KEY(companyId ?? "", contactId ?? ""),
    queryFn: () =>
      companiesApi
        .getContactPerson(companyId as string, contactId as string)
        .then(({ data }) => data),
    enabled,
  });
}

export function useCreateContactPerson() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      companyId,
      request,
    }: {
      companyId: string;
      request: CreateContactPersonRequest;
    }) => companiesApi.createContactPerson(companyId, request).then(({ data }) => data),
    onSuccess: (_data, { companyId }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-contact-persons", companyId] });
    },
  });
}

export function useUpdateContactPerson() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      companyId,
      contactId,
      request,
    }: {
      companyId: string;
      contactId: string;
      request: UpdateContactPersonRequest;
    }) =>
      companiesApi
        .updateContactPerson(companyId, contactId, request)
        .then(({ data }) => data),
    onSuccess: (_data, { companyId, contactId }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-contact-persons", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACT_PERSON_QUERY_KEY(companyId, contactId) });
    },
  });
}

export function useDeactivateContactPerson() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ companyId, contactId }: { companyId: string; contactId: string }) =>
      companiesApi.deactivateContactPerson(companyId, contactId),
    onSuccess: (_data, { companyId, contactId }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-contact-persons", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACT_PERSON_QUERY_KEY(companyId, contactId) });
    },
  });
}

export function useReactivateContactPerson() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ companyId, contactId }: { companyId: string; contactId: string }) =>
      companiesApi.reactivateContactPerson(companyId, contactId),
    onSuccess: (_data, { companyId, contactId }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-contact-persons", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACT_PERSON_QUERY_KEY(companyId, contactId) });
    },
  });
}

export function useReassignContactPerson() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      companyId,
      contactId,
      request,
    }: {
      companyId: string;
      contactId: string;
      request: ReassignContactPersonRequest;
    }) =>
      companiesApi
        .reassignContactPerson(companyId, contactId, request)
        .then(({ data }) => data),
    onSuccess: (_data, { companyId, contactId }) => {
      queryClient.invalidateQueries({ queryKey: ["selling-contact-persons", companyId] });
      queryClient.invalidateQueries({ queryKey: CONTACT_PERSON_QUERY_KEY(companyId, contactId) });
    },
  });
}

export function useExportCompanyContacts() {
  return useMutation({
    mutationFn: ({
      companyId,
      query,
    }: {
      companyId: string;
      query: ContactPersonsExportQuery;
    }) => companiesApi.exportCompanyContacts(companyId, query),
  });
}
