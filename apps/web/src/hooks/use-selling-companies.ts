"use client";

import { useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { companiesApi } from "@/lib/selling/companies.api";
import type { BaseSelectOption } from "@/components/ui/base-select";
import type {
  CompaniesQuery,
  CreateCompanyRequest,
  UpdateCompanyRequest,
  CreateContactPersonRequest,
  UpdateContactPersonRequest,
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

export const COMPANY_QUERY_KEY = (id: string) => ["selling-company", id] as const;

export function useCompany(id: string | null | undefined, options?: { enabled?: boolean }) {
  const enabled = (options?.enabled ?? true) && Boolean(id);
  return useQuery({
    queryKey: COMPANY_QUERY_KEY(id ?? ""),
    queryFn: () => companiesApi.getCompany(id as string).then(({ data }) => data),
    enabled,
  });
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
