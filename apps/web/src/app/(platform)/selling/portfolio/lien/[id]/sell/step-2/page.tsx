"use client";

import { useParams } from "next/navigation";
import ReviewDocumentsStep from "@/components/selling/sell-lien-wizard/review-documents-step";

export default function SellLienStep2Page() {
  const params = useParams<{ id: string }>();
  return <ReviewDocumentsStep lienId={params?.id ?? ""} />;
}
