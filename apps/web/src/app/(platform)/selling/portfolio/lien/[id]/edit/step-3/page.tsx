"use client";

import { useParams } from "next/navigation";
import MedicalCodesStep from "@/components/selling/lien-wizard/medical-codes-step";

export default function EditLienStep3Page() {
  const params = useParams<{ id: string }>();
  return <MedicalCodesStep lienId={params?.id ?? ""} />;
}
