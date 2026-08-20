"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
import MedicalCodesDescription from "../forms/add-medical-lien/medical-codes-description";
import { LienWizardShell } from "./shell";
import { buildFormsFromLien, goToStep } from "./shared";
import { SkeletonField, SkeletonTable } from "@/components/lien/skeleton-loader";

// Mirrors MedicalCodesDescription's layout: title + description, the
// code/billing/target-amount entry fields, and the pricing rows table.
function MedicalCodesStepSkeleton() {
  return (
    <div className="space-y-4 animate-pulse pt-5">
      <div className="h-6 bg-gray-100 rounded w-72" />
      <div className="h-3 bg-gray-100 rounded w-full max-w-lg" />
      <SkeletonField full />
      <SkeletonField full />
      <div className="flex gap-3 items-end">
        <div className="flex-1">
          <SkeletonField full />
        </div>
        <div className="flex-1">
          <SkeletonField full />
        </div>
        <div className="h-9 w-20 bg-gray-100 rounded-lg shrink-0" />
      </div>
      <SkeletonTable rows={3} cols={4} />
    </div>
  );
}

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
        toast.error(err instanceof Error ? err.message : "Failed to load lien");
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
        toast.error(err.message);
      } else {
        toast.error("An unexpected error occurred");
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <LienWizardShell
      step={3}
      hydrating={hydrating}
      skeleton={<MedicalCodesStepSkeleton />}
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
