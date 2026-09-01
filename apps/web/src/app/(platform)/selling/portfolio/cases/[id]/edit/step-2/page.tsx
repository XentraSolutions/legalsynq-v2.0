"use client";

import { useParams } from "next/navigation";
import PlaintiffInfoStep from "@/components/selling/case-wizard/plaintiff-info-step";

export default function EditCaseStep2Page() {
  const params = useParams<{ id: string }>();
  return <PlaintiffInfoStep caseId={params?.id ?? ""} />;
}
