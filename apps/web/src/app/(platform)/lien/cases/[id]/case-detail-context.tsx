"use client";

import { createContext, useContext } from "react";
import type {
  CaseDetail,
  CaseLienItem,
  CaseLienItemMetadata,
} from "@/lib/cases";
import type { PanelMode } from "@/components/lien/layout-split";
import type { DropdownOption } from "@/lib/lookup/lookup.types";
import type { PaginationMeta } from "@/lib/billofsale";

export type CaseDetailContextValue = {
  id: string;
  d: CaseDetail;
  caseUpdates: any;
  documentTypes: DropdownOption[];
  relatedLiens: (CaseLienItem & CaseLienItemMetadata)[];
  liensPagination: PaginationMeta;
  liensLoadedAt: Date | null;
  refetchLiens: () => void;
  isLiensFetching: boolean;
  casePayments: import("@/lib/settlement/settlement.types").LegacyCasePayment[];
  paymentsLoadedAt: Date | null;
  refetchPayments: () => Promise<unknown>;
  isPaymentsFetching: boolean;
  canEdit: boolean;
  panelMode: PanelMode;
  setPanelMode: (m: PanelMode) => void;
  openMedicalLienModal: (open: boolean) => void;
};

const CaseDetailContext = createContext<CaseDetailContextValue | null>(null);

export function CaseDetailContextProvider({
  value,
  children,
}: {
  value: CaseDetailContextValue;
  children: React.ReactNode;
}) {
  return (
    <CaseDetailContext.Provider value={value}>
      {children}
    </CaseDetailContext.Provider>
  );
}

export function useCaseDetailContext(): CaseDetailContextValue {
  const ctx = useContext(CaseDetailContext);
  if (!ctx) {
    throw new Error(
      "useCaseDetailContext must be used within a CaseDetailContextProvider",
    );
  }
  return ctx;
}
