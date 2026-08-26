"use client";

import { useParams } from "next/navigation";
import PlaintiffInfoStep from "@/components/selling/case-wizard/plaintiff-info-step";

export default function CaseDraftStep2Page() {
  const params = useParams<{ draftId: string }>();
  return <PlaintiffInfoStep draftId={params?.draftId ?? ""} />;
}
