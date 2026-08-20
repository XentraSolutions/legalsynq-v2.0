"use client";

import { useRouter } from "next/navigation";
import { toast } from "sonner";
import UploadDocuments from "../forms/add-medical-lien/medical-upload-document";
import { LienWizardShell } from "./shell";
import { goToStep } from "./shared";

export interface UploadDocsStepProps {
  lienId: string;
  caseId?: string;
}

// Step 4 — always edits an existing lien. Documents are attached (and
// persisted via saveDocuments) as soon as each file uploads inside
// UploadDocuments itself, so there's nothing left to save on Continue —
// it just confirms and returns to the portfolio.
export default function UploadDocsStep({ lienId, caseId }: UploadDocsStepProps) {
  const router = useRouter();

  const handleContinue = () => {
    toast.success("Lien added successfully.");
    router.push("/selling/portfolio");
  };

  return (
    <LienWizardShell
      step={4}
      continueLabel="Finish"
      onBack={() => goToStep(router, lienId, 3)}
      onContinue={handleContinue}
    >
      <UploadDocuments caseId={caseId} lienId={lienId} onUploaded={() => {}} />
    </LienWizardShell>
  );
}
