"use client";

import { useParams } from "next/navigation";
import AddLienComponent from "@/components/selling/add-lien-component";

export default function AddLienResumePage() {
  const params = useParams<{ lienId: string }>();
  return <AddLienComponent lienId={params?.lienId ?? ""} />;
}
