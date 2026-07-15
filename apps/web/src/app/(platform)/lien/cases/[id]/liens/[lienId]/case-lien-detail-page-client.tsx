"use client";

import { useRouter } from "next/navigation";
import { LayoutSplit } from "@/components/lien/layout-split";
import { useCaseDetailContext } from "../../case-detail-context";
import { EmailSection } from "../../components/email-section";
import { SmsSection } from "../../components/sms-section";
import { ContactsSection } from "../../components/contacts-section";
import { LienDetailView } from "../../tabs/liens/lien-detail-view";

export function CaseLienDetailPageClient({
  caseId,
  lienId,
}: {
  caseId: string;
  lienId: string;
}) {
  const router = useRouter();
  const { d, panelMode, setPanelMode } = useCaseDetailContext();

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
        <div className="space-y-4">
          <EmailSection />
          <SmsSection />
          <ContactsSection
            items={[
              {
                icon: "ri-building-line",
                iconBgClass: "bg-blue-50",
                iconColorClass: "text-blue-500",
                name: d.insuranceCarrier || "",
                role: "Law Firm",
              },
            ]}
          />
        </div>
      }
      mode={panelMode}
      onModeChange={setPanelMode}
    />
  );
}
