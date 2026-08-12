"use client";

import { useParams } from "next/navigation";
import LienInfoStep from "@/components/selling/lien-wizard/lien-info-step";

export default function EditLienStep1Page() {
  const params = useParams<{ id: string }>();
  return <LienInfoStep lienId={params?.id ?? ""} />;
}
