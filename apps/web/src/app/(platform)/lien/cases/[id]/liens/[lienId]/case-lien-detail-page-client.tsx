"use client";

import { useRouter } from "next/navigation";
import { LayoutSplit } from "@/components/lien/layout-split";
import { useCaseDetailContext } from "../../case-detail-context";
import { FeedsSection } from "../../components/feeds-section";
import { LienDetailView } from "../../tabs/liens/lien-detail-view";

export function CaseLienDetailPageClient({
  caseId,
  lienId,
}: {
  caseId: string;
  lienId: string;
}) {
  const router = useRouter();
  const { panelMode, setPanelMode } = useCaseDetailContext();

  return (
    <LayoutSplit
      left={
        <LienDetailView
          caseId={caseId}
          lienId={lienId}
          onGoBack={() => router.push(`/lien/cases/${caseId}/liens`)}
        />
      }
      right={
        <FeedsSection caseId={caseId} panelMode={panelMode} onPanelModeChange={setPanelMode} />
      }
      mode={panelMode}
      onModeChange={setPanelMode}
      showControls={false}
    />
  );
}
