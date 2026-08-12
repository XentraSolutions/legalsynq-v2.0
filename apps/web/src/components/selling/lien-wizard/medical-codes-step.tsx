"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import MedicalCodesDescription from "../forms/add-medical-lien/medical-codes-description";
import { LienWizardShell } from "./shell";
import { buildFormsFromLien, goToStep } from "./shared";

export interface MedicalCodesStepProps {
  lienId: string;
  caseId?: string;
}

// Step 3 — always edits an existing lien (see FundingCompanyStep).
export default function MedicalCodesStep({
  lienId,
  caseId,
}: MedicalCodesStepProps) {
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
        setFormData(buildFormsFromLien(lien).medicalCodes);
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
      const rows = formData?.codeRows ?? [];
      const askAmount = rows.reduce(
        (sum: number, row: any) => sum + (row.targetSaleAmount || 0),
        0,
      );
      const totalBillingAmount = rows.reduce(
        (sum: number, row: any) => sum + (row.billingAmount || 0),
        0,
      );

      await liensService.saveMedicalPricing(lienId, {
        askAmount,
        billingAmount: totalBillingAmount,
        rows: rows.map((row: any) => ({
          medicalCode: row.code,
          description: row.description || undefined,
          billingAmount: row.billingAmount,
          medicareCost: row.medicareCost,
          targetSaleAmount: row.targetSaleAmount,
        })),
      });
      goToStep(router, lienId, 4);
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
      step={3}
      hydrating={hydrating}
      submitting={submitting}
      continueDisabled={!formValid}
      onBack={() => goToStep(router, lienId, 2)}
      onContinue={handleContinue}
    >
      <MedicalCodesDescription
        caseId={caseId}
        lienId={lienId}
        data={formData}
        onFormValid={onFormValid}
      />
    </LienWizardShell>
  );
}
