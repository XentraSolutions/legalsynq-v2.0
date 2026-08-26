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
import { useCreateCaseDraft, useFinalizeCaseDraft } from "@/hooks/selling/use-case-drafts";

export function CaseWizard() {
  const router = useRouter();
  const [step, setStep] = useState<1 | 2>(1);
  const [caseInfo, setCaseInfo] = useState<CaseInfoFieldsValue>(
    CASE_INFO_INITIAL_FORM,
  );
  const [plaintiffInfo, setPlaintiffInfo] = useState<PlaintiffInfoFieldsValue>(
    PLAINTIFF_INFO_INITIAL_FORM,
  );
  const [draftId, setDraftId] = useState<string | null>(null);
  const [createdCaseId, setCreatedCaseId] = useState<string | null>(null);
  const [createdCaseNumber, setCreatedCaseNumber] = useState<string | null>(
    null,
  );
  const createCaseDraft = useCreateCaseDraft();
  const finalizeCaseDraft = useFinalizeCaseDraft();

  const handleStep1Continue = async (data: CaseInfoFieldsValue) => {
    setCaseInfo(data);
    try {
      const draft = await createCaseDraft.mutateAsync({
        caseStatus: data.caseStatusId,
        accidentTypeId: data.accidentTypeId,
        accidentState: data.accidentStateId,
        handlingLawFirmId: data.lawfirmId,
        // Omit rather than send "" for these optional fields — the backend's
        // GUID/date model binders 400 on an empty string instead of treating
        // it as absent.
        ...(data.dateOfLoss && { dateOfLoss: data.dateOfLoss }),
        ...(data.caseManagerId && { caseManagerId: data.caseManagerId }),
        ...(data.notes && { caseTrackingNotes: data.notes }),
      });
      setDraftId(draft.draftId);
      setStep(2);
    } catch {
      toast.error("Failed to save case information");
    }
  };

  const handleStep2Continue = async (data: PlaintiffInfoFieldsValue) => {
    setPlaintiffInfo(data);
    if (!draftId) return;
    try {
      const finalized = await finalizeCaseDraft.mutateAsync({
        draftId,
        request: {
          firstName: data.firstName,
          lastName: data.lastName,
          birthdate: data.birthdate,
          email: data.email,
          phone: data.phone,
          gender: data.sex,
          address: data.address,
          city: data.city,
          state: data.state,
          zipcode: data.zipcode,
        },
      });
      setCreatedCaseId(finalized.caseId);
      setCreatedCaseNumber(finalized.caseNumber);
    } catch {
      toast.error("Failed to create case");
    }
  };

  if (step === 1) {
    return (
      <CaseInfoStep
        data={caseInfo}
        submitting={createCaseDraft.isPending}
        onBack={() => router.back()}
        onContinue={handleStep1Continue}
      />
    );
  }

  return (
    <>
      <PlaintiffInfoStep
        data={plaintiffInfo}
        submitting={finalizeCaseDraft.isPending}
        onBack={() => setStep(1)}
        onContinue={handleStep2Continue}
      />
      <NewCaseAddedModal
        open={!!createdCaseId}
        caseNumber={createdCaseNumber ?? undefined}
        onClose={() => router.push("/selling/portfolio/cases")}
        onAddLien={() =>
          router.push(`/selling/portfolio/lien/add?caseId=${createdCaseId}`)
        }
      />
    </>
  );
}
