"use client";

import { useParams } from "next/navigation";
import CaseInfoStep from "@/components/selling/case-wizard/case-info-step";

export default function CaseDraftStep1Page() {
  const params = useParams<{ draftId: string }>();
  return <CaseInfoStep draftId={params?.draftId ?? ""} />;
}
