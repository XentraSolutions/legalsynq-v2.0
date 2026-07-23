"use client";

<<<<<<< Updated upstream
import { LayoutSplit, PanelMode } from "@/components/lien/layout-split";
import { FeedsSection } from "../../components/feeds-section";
import { CaseDetail, casesService } from "@/lib/cases";
import { CollapsibleSection } from "../../components/collapsible-section";
import { useEffect, useState } from "react";
import { DateDisplay } from "@/components/ui/date-display";
import { emailToDisplayName } from "@/lib/liens/note-utils";
=======
import { useEffect, useState } from "react";
import type { PanelMode } from "@/components/lien/layout-split";
import type { CaseDetail } from "@/lib/cases";
import {
  lienCaseNotesLegacyService,
  type CaseFeedNote,
} from "@/lib/liens/lien-case-notes-legacy.service";
>>>>>>> Stashed changes

export function NotesTab({
  caseDetail,
}: {
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  onPanelModeChange: (mode: PanelMode) => void;
}) {
<<<<<<< Updated upstream
  const [notes, setNotes] = useState<any>([]);
  const [loading, setIsLoading] = useState<boolean>(false);
  const fetchNotes = async () => {
    setIsLoading(true);
    try {
      const response = await casesService.getCaseNotes(caseDetail.id);
      setNotes(response);
    } catch (err) {
      console.log(err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchNotes();
  }, []);
  const rightContent = (
    <FeedsSection
      caseId={caseDetail.id}
      panelMode={panelMode}
      onPanelModeChange={onPanelModeChange}
    />
  );
  const leftContent = (
    <CollapsibleSection title="Notes" icon="ri-compass-3-line">
      <div className="space-y-4">
        <div className="pt-3">
          {loading ? (
            <div className="flex justify-center py-6">
              <i className="ri-loader-4-line animate-spin text-gray-400 text-lg" />
            </div>
          ) : notes.length === 0 ? (
            <div className="min-h-30 flex items-center justify-center border-b border-gray-100">
              <p className="text-sm text-center flex justify-center text-gray-400 leading-relaxed">
                No Notes
              </p>
            </div>
          ) : (
            <div className="space-y-2 max-h-72 overflow-y-auto">
              {notes.map((note: any) => (
                <div
                  key={note.id}
                  className="group border-b border-gray-100 px-3 py-2.5"
                >
                  <div className="flex items-start gap-3">
                    <div className="w-9 h-9 rounded-full bg-gray-200 flex items-center justify-center shrink-0">
                      <i className="ri-user-line text-gray-400 text-base" />
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-start justify-between gap-2">
                        <p className="text-sm font-semibold text-gray-800 truncate">
                          {emailToDisplayName(note.createdBy)}
                        </p>
                        <p className="text-xs text-gray-400">
                          <DateDisplay value={note.created} format="datetime" />
                        </p>
                      </div>
                      <p className="mt-2 text-sm text-gray-700 whitespace-pre-wrap break-words">
                        {note.note}
                      </p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </CollapsibleSection>
  );
=======
  const [notes, setNotes] = useState<CaseFeedNote[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isCurrent = true;

    void lienCaseNotesLegacyService
      .getCaseNotes(caseDetail.id)
      .then((items) => {
        if (!isCurrent) return;
        setNotes(items);
        setError(null);
      })
      .catch(() => {
        if (!isCurrent) return;
        setNotes([]);
        setError("Unable to load note history.");
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [caseDetail.id]);
>>>>>>> Stashed changes

  return (
    <section className="overflow-hidden rounded-xl border border-gray-200 bg-white">
      <header className="border-b border-gray-200 px-7 py-5">
        <h2 className="text-lg font-semibold text-slate-900">Notes</h2>
      </header>

      {isLoading ? (
        <div className="px-7 py-12 text-sm text-gray-400">Loading notes...</div>
      ) : error ? (
        <div className="px-7 py-12 text-sm text-red-500">{error}</div>
      ) : notes.length === 0 ? (
        <div className="px-7 py-12 text-sm text-gray-400">No notes found.</div>
      ) : (
        <div className="px-6">
          {notes.map((note) => (
            <article
              key={note.id}
              className="flex items-start gap-3 border-b border-gray-200 py-5 last:border-b-0"
            >
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-400">
                <i className="ri-user-line text-sm" />
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-start justify-between gap-4">
                  <p className="text-sm font-semibold text-slate-800">
                    {note.createdBy || "Unknown User"}
                  </p>
                  <time className="shrink-0 text-xs text-slate-400">
                    {note.created}
                  </time>
                </div>
                <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">
                  {note.note}
                </p>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
