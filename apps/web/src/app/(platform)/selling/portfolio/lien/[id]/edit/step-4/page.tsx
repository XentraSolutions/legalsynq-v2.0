"use client";

import { useParams } from "next/navigation";
import UploadDocsStep from "@/components/selling/lien-wizard/upload-docs-step";

export default function EditLienStep4Page() {
  const params = useParams<{ id: string }>();
  return <UploadDocsStep lienId={params?.id ?? ""} />;
}
