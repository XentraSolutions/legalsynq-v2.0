"use client";

import { useParams } from "next/navigation";
import { SellLienWizard } from "@/components/selling/sell-lien-wizard";

export default function SellLienPage() {
  const params = useParams<{ id: string }>();
  return <SellLienWizard lienId={params?.id ?? ""} />;
}
