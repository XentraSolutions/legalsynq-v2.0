"use client";

import { useParams } from "next/navigation";
import CaseInfoStep from "@/components/selling/case-wizard/case-info-step";

export default function EditCaseStep1Page() {
  const params = useParams<{ id: string }>();
  return <CaseInfoStep caseId={params?.id ?? ""} />;
}
