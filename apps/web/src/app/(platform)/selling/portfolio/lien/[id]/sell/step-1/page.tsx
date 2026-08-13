"use client";

import { useParams } from "next/navigation";
import BuyerSelectionStep from "@/components/selling/sell-lien-wizard/buyer-selection-step";

export default function SellLienStep1Page() {
  const params = useParams<{ id: string }>();
  return <BuyerSelectionStep lienId={params?.id ?? ""} />;
}
