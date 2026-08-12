"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { NotesTab } from "../tabs/notes/notes-tab";

export default function CaseNotesPage() {
  const { d, panelMode, setPanelMode } = useCaseDetailContext();
  return (
    <NotesTab
      caseDetail={d}
      panelMode={panelMode}
      onPanelModeChange={setPanelMode}
    />
  );
}
