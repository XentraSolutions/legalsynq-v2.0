"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { DetailsTab } from "../tabs/details/details-tab";

export default function CaseDetailsPage() {
  const { d, caseUpdates, panelMode, setPanelMode, canEdit, fetchCase } =
    useCaseDetailContext();

  return (
    <DetailsTab
      d={d}
      u={caseUpdates}
      panelMode={panelMode}
      onPanelModeChange={setPanelMode}
      canEdit={canEdit}
      onCaseUpdated={() => fetchCase()}
    />
  );
}
