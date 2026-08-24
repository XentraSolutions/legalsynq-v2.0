"use client";

import { createContext, useContext } from "react";
import type { CompanyDetail, CompanyDetailsSummary } from "@/lib/selling/companies.types";
import type { UseQueryResult } from "@tanstack/react-query";

export type CompanyDetailContextValue = {
  id: string;
  company: CompanyDetail;
  canEdit: boolean;
  detailsQuery: UseQueryResult<CompanyDetailsSummary>;
  recentCasesPage: number;
  setRecentCasesPage: (page: number) => void;
};

const CompanyDetailContext = createContext<CompanyDetailContextValue | null>(null);

export function CompanyDetailContextProvider({
  value,
  children,
}: {
  value: CompanyDetailContextValue;
  children: React.ReactNode;
}) {
  return (
    <CompanyDetailContext.Provider value={value}>{children}</CompanyDetailContext.Provider>
  );
}

export function useCompanyDetailContext(): CompanyDetailContextValue {
  const ctx = useContext(CompanyDetailContext);
  if (!ctx) {
    throw new Error(
      "useCompanyDetailContext must be used within a CompanyDetailContextProvider",
    );
  }
  return ctx;
}
