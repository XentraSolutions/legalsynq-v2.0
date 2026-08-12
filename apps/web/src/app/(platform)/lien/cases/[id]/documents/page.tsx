"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { DocumentsTab } from "../tabs/documents/documents-tab";

export default function CaseDocumentsPage() {
  const { id, documentTypes, d, panelMode, setPanelMode } =
    useCaseDetailContext();

  return (
    <DocumentsTab
      docTypes={documentTypes}
      caseDetail={d}
      panelMode={panelMode}
      lienid={id}
      onPanelModeChange={setPanelMode}
    />
  );
}
