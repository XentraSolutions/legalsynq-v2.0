"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { liensService } from "@/lib/selling";
import type { BaseSelectOption } from "@/components/ui/base-select";

function toOptions(items: { id: string; name: string }[]): BaseSelectOption[] {
  return items.map((item) => ({ value: item.id, label: item.name }));
}

export const SELLING_FUNDING_COMPANIES_QUERY_KEY = ["selling-funding-companies"] as const;

export function useSellingFundingCompanies(options?: { enabled?: boolean }) {
  const query = useQuery({
    queryKey: SELLING_FUNDING_COMPANIES_QUERY_KEY,
    queryFn: () => liensService.getFundingCompanies(),
    staleTime: 30_000,
    enabled: options?.enabled,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}

export const SELLING_FUNDING_COMPANY_CONTACTS_QUERY_KEY = (fundingCompanyId: string) =>
  ["selling-funding-company-contacts", fundingCompanyId] as const;

export function useSellingFundingCompanyContacts(
  fundingCompanyId: string | null | undefined,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(fundingCompanyId);
  const query = useQuery({
    queryKey: SELLING_FUNDING_COMPANY_CONTACTS_QUERY_KEY(fundingCompanyId ?? ""),
    queryFn: () => liensService.getFundingCompanyContacts(fundingCompanyId as string),
    enabled,
    staleTime: 30_000,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}

export const SELLING_FACILITIES_QUERY_KEY = ["selling-facilities"] as const;

export function useSellingFacilities(options?: { enabled?: boolean }) {
  const query = useQuery({
    queryKey: SELLING_FACILITIES_QUERY_KEY,
    queryFn: () => liensService.getFacilities(),
    staleTime: 30_000,
    enabled: options?.enabled,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}

export const SELLING_LAW_FIRMS_QUERY_KEY = ["selling-law-firms"] as const;

export function useSellingLawFirms(options?: { enabled?: boolean }) {
  const query = useQuery({
    queryKey: SELLING_LAW_FIRMS_QUERY_KEY,
    queryFn: () => liensService.getLawFirms(),
    staleTime: 30_000,
    enabled: options?.enabled,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}

export const SELLING_CASE_MANAGERS_QUERY_KEY = (lawFirmId: string) =>
  ["selling-case-managers", lawFirmId] as const;

export function useSellingCaseManagers(
  lawFirmId: string | null | undefined,
  options?: { enabled?: boolean },
) {
  const enabled = (options?.enabled ?? true) && Boolean(lawFirmId);
  const query = useQuery({
    queryKey: SELLING_CASE_MANAGERS_QUERY_KEY(lawFirmId ?? ""),
    queryFn: () => liensService.getCaseManagers(lawFirmId as string),
    enabled,
    staleTime: 30_000,
  });
  return { ...query, options: useMemo(() => toOptions(query.data ?? []), [query.data]) };
}
