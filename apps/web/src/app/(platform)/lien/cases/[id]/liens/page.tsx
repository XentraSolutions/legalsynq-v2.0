"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { LiensTab } from "../tabs/liens/liens-tab";

export default function CaseLiensPage() {
  const {
    id,
    relatedLiens,
    liensPagination,
    d,
    panelMode,
    setPanelMode,
    openMedicalLienModal,
  } = useCaseDetailContext();

  return (
    <LiensTab
      caseId={id}
      liens={relatedLiens}
      liensPagination={liensPagination}
      caseDetail={d}
      panelMode={panelMode}
      onPanelModeChange={setPanelMode}
      onAddMedicalLien={openMedicalLienModal}
    />
  );
}
