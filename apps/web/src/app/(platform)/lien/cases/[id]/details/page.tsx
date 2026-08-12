"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useCaseDetailContext } from "../case-detail-context";
import { DetailsTab } from "../tabs/details/details-tab";

export default function CaseDetailsPage() {
  const { d, caseUpdates, panelMode, setPanelMode, canEdit } =
    useCaseDetailContext();
  const queryClient = useQueryClient();

  return (
    <DetailsTab
      d={d}
      u={caseUpdates}
      panelMode={panelMode}
      onPanelModeChange={setPanelMode}
      canEdit={canEdit}
      onCaseUpdated={() => {
        queryClient.invalidateQueries({
          queryKey: ["caseDetail", d.id],
        });
      }}
    />
  );
}
