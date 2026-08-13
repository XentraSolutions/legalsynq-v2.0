"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import FundingCompanyInfo from "../forms/add-medical-lien/funding-company-info";
import { LienWizardShell } from "./shell";
import { buildFormsFromLien, goToStep } from "./shared";

export interface FundingCompanyStepProps {
  lienId: string;
  caseId?: string;
}

// Step 2 — always edits an existing lien (the route requires an id; a lien
// is only created by completing step 1 first).
export default function FundingCompanyStep({
  lienId,
  caseId,
}: FundingCompanyStepProps) {
  const router = useRouter();
  const { show: showToast } = useToast();
  const [hydrating, setHydrating] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [formData, setFormData] = useState<any>(null);
  const [formValid, setFormValid] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const lien = await liensService.getLienById(lienId);
        if (cancelled) return;
        setFormData(buildFormsFromLien(lien).fundingCompany);
      } catch (err) {
        showToast(
          err instanceof Error ? err.message : "Failed to load lien",
          "error",
        );
      } finally {
        if (!cancelled) setHydrating(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // Mount-only: lienId is fixed for the lifetime of this page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function onFormValid(isValid: boolean, data?: any) {
    setFormValid(!!isValid);
    if (data !== undefined) setFormData(data);
  }

  const handleContinue = async () => {
    setSubmitting(true);
    try {
      await liensService.saveCaseInformation(lienId, {
        fundingCompanyId: formData?.fundingCompanyId || undefined,
        fundingCompanyContactId:
          formData?.fundingCompanyContactId || undefined,
        medicalProviderId: formData?.medicalProviderId || undefined,
        handlingLawFirmId: formData?.lawfirmId || undefined,
        caseManagerId: formData?.caseManagerId || undefined,
        caseId: caseId || undefined,
        createCaseIfMissing: !caseId,
      });
      goToStep(router, lienId, 3);
    } catch (err) {
      if (err instanceof ApiError) {
        showToast(err.message, "error");
      } else {
        showToast("An unexpected error occurred", "error");
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <LienWizardShell
      step={2}
      hydrating={hydrating}
      submitting={submitting}
      continueDisabled={!formValid}
      onBack={() => goToStep(router, lienId, 1)}
      onContinue={handleContinue}
    >
      <FundingCompanyInfo
        caseId={caseId}
        lienId={lienId}
        data={formData}
        onFormValid={onFormValid}
      />
    </LienWizardShell>
  );
}
