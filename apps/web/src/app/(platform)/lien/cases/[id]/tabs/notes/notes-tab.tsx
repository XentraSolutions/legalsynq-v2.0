"use client";

import { LayoutSplit, PanelMode } from "@/components/lien/layout-split";
import { FeedsSection } from "../../components/feeds-section";
import { CaseDetail } from "@/lib/cases";
import { CollapsibleSection } from "../../components/collapsible-section";

export function NotesTab({
  caseDetail,
  panelMode,
  onPanelModeChange,
}: {
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
}) {
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
        {caseDetail.notes ? (
          <div className="min-h-30 border-b border-gray-100">
            <p className="text-sm text-gray-600 leading-relaxed">
              {caseDetail.notes}
            </p>
          </div>
        ) : (
          <div className="min-h-30 flex items-center justify-center border-b border-gray-100">
            <p className="text-sm text-center flex justify-center text-gray-400 leading-relaxed">
              No Notes
            </p>
          </div>
        )}
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
