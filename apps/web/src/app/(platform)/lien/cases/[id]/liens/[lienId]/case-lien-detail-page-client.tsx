"use client";

import { useRouter } from "next/navigation";
import { LayoutSplit } from "@/components/lien/layout-split";
import { useCaseDetailContext } from "../../case-detail-context";
import { FeedsSection } from "../../components/feeds-section";
import { LienDetailView } from "../../tabs/liens/lien-detail-view";

import { useQueryClient } from "@tanstack/react-query";
export function CaseLienDetailPageClient({
  caseId,
  lienId,
}: {
  caseId: string;
  lienId: string;
}) {
  const router = useRouter();
  const { panelMode, setPanelMode } = useCaseDetailContext();
  const queryClient = useQueryClient();

  const handleUpdate = async () => {
    queryClient.invalidateQueries({
      queryKey: ["case-liens", caseId],
    });
    queryClient.invalidateQueries({
      queryKey: ["lien-updates", caseId],
    });

    queryClient.invalidateQueries({
      queryKey: ["case-updates", caseId],
    });
    router.push(`/lien/cases/${caseId}/liens`);
  };
  return (
    <LayoutSplit
      left={
        <LienDetailView
          caseId={caseId}
          lienId={lienId}
          onUpdate={handleUpdate}
          onGoBack={() => router.back()}
        />
      }
      right={
        <FeedsSection
          caseId={caseId}
          panelMode={panelMode}
          onPanelModeChange={setPanelMode}
        />
      }
      mode={panelMode}
      onModeChange={setPanelMode}
      showControls={false}
    />
  );
}
