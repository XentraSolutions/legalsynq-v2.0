"use client";

import { LayoutSplit, PanelMode } from "@/components/lien/layout-split";
import { FeedsSection } from "../../components/feeds-section";
import { CaseDetail, casesService } from "@/lib/cases";
import { CollapsibleSection } from "../../components/collapsible-section";
import { useEffect, useState } from "react";
import { DateDisplay } from "@/components/ui/date-display";
import { emailToDisplayName } from "@/lib/liens/note-utils";

export function NotesTab({
  caseDetail,
  panelMode,
  onPanelModeChange,
}: {
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
}) {
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
    <CollapsibleSection title="Case Tracking Notes" icon="ri-compass-3-line">
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

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
      showControls={false}
    />
  );
}
