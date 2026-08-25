"use client";

import { useSearchParams } from "next/navigation";
import LienInfoStep from "@/components/selling/lien-wizard/lien-info-step";

export default function AddLienPage() {
  const searchParams = useSearchParams();
  const caseId = searchParams.get("caseId") ?? undefined;
  return <LienInfoStep caseId={caseId} />;
}
