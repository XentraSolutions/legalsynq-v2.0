"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import CaseInfoStep, { CASE_INFO_INITIAL_FORM } from "./case-info-step";
import PlaintiffInfoStep, {
  PLAINTIFF_INFO_INITIAL_FORM,
} from "./plaintiff-info-step";
import { NewCaseAddedModal } from "./new-case-added-modal";
import type { CaseInfoFieldsValue } from "../forms/add-case/case-info-fields";
import type { PlaintiffInfoFieldsValue } from "../forms/add-case/plaintiff-info-fields";

// Case creation isn't wired to a real endpoint yet — there's no
// selling-scoped case-create API today (the legacy casesApi.create /
// CreateCaseRequestDto in @/lib/cases is a different, non-selling surface
// and is intentionally left unwired here). Both steps collect real state and
// validate normally; "Add Case" below just fakes a short delay and a local
// id so the "New Case Added" hand-off into Add Lien has something to pass
// along. Swap this for a real casesService.createCase(...) call once a
// selling case-create endpoint exists — the field names already line up
// almost 1:1 with CreateCaseRequestDto.
async function stubCreateCase(
  _caseInfo: CaseInfoFieldsValue,
  _plaintiffInfo: PlaintiffInfoFieldsValue,
): Promise<{ id: string }> {
  await new Promise((resolve) => setTimeout(resolve, 400));
  return { id: `draft-${Date.now()}` };
}

export function CaseWizard() {
  const router = useRouter();
  const [step, setStep] = useState<1 | 2>(1);
  const [caseInfo, setCaseInfo] = useState<CaseInfoFieldsValue>(
    CASE_INFO_INITIAL_FORM,
  );
  const [plaintiffInfo, setPlaintiffInfo] = useState<PlaintiffInfoFieldsValue>(
    PLAINTIFF_INFO_INITIAL_FORM,
  );
  const [submitting, setSubmitting] = useState(false);
  const [createdCaseId, setCreatedCaseId] = useState<string | null>(null);

  const handleStep1Continue = (data: CaseInfoFieldsValue) => {
    setCaseInfo(data);
    setStep(2);
  };

  const handleStep2Continue = async (data: PlaintiffInfoFieldsValue) => {
    setPlaintiffInfo(data);
    setSubmitting(true);
    try {
      const created = await stubCreateCase(caseInfo, data);
      setCreatedCaseId(created.id);
    } catch {
      toast.error("Failed to create case");
    } finally {
      setSubmitting(false);
    }
  };

  if (step === 1) {
    return (
      <CaseInfoStep
        data={caseInfo}
        onBack={() => router.back()}
        onContinue={handleStep1Continue}
      />
    );
  }

  return (
    <>
      <PlaintiffInfoStep
        data={plaintiffInfo}
        submitting={submitting}
        onBack={() => setStep(1)}
        onContinue={handleStep2Continue}
      />
      <NewCaseAddedModal
        open={!!createdCaseId}
        onClose={() => router.push("/selling/portfolio/cases")}
        onAddLien={() =>
          router.push(`/selling/portfolio/lien/add?caseId=${createdCaseId}`)
        }
      />
    </>
  );
}
