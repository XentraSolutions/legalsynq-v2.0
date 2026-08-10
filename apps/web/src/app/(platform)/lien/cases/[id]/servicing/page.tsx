"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { ServicingTab } from "../tabs/servicing/servicing-tab";

export default function CaseServicingPage() {
  const {
    d,
    relatedLiens,
    liensLoadedAt,
    refetchLiens,
    isLiensFetching,
    casePayments,
    paymentsLoadedAt,
    refetchPayments,
    isPaymentsFetching,
    panelMode,
    setPanelMode,
  } = useCaseDetailContext();

  return (
    <ServicingTab
      caseDetail={d}
      liensList={relatedLiens}
      liensLoadedAt={liensLoadedAt}
      onRefreshLiens={refetchLiens}
      isLiensFetching={isLiensFetching}
      payments={casePayments}
      paymentsLoadedAt={paymentsLoadedAt}
      onRefreshPayments={refetchPayments}
      isPaymentsFetching={isPaymentsFetching}
      panelMode={panelMode}
      onPanelModeChange={setPanelMode}
    />
  );
}
