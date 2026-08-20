"use client";

import { useParams } from "next/navigation";
import FundingCompanyStep from "@/components/selling/lien-wizard/funding-company-step";

export default function EditLienStep2Page() {
  const params = useParams<{ id: string }>();
  return <FundingCompanyStep lienId={params?.id ?? ""} />;
}
