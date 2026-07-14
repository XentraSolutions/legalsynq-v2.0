"use client";

import { useCaseDetailContext } from "../case-detail-context";
import { NotesTab } from "../tabs/notes/notes-tab";

export default function CaseNotesPage() {
  const { id } = useCaseDetailContext();
  return <NotesTab caseId={id} />;
}
