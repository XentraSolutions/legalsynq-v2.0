"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
import FundingCompanyInfo from "../forms/add-medical-lien/funding-company-info";
import { LienWizardShell } from "./shell";
import {
  buildFormsFromLien,
  goToStep,
  detailHref,
  DETAIL_EDIT_PARAM,
  DETAIL_EDIT_VALUE,
} from "./shared";
import { SkeletonFormGrid } from "@/components/lien/skeleton-loader";

// Mirrors FundingCompanyInfo's layout: title + description and the lien-owned
// medical-provider, funding-company, and contact selections.
function FundingCompanyStepSkeleton() {
  return (
    <div className="space-y-4 animate-pulse pt-5">
      <div className="h-6 bg-gray-100 rounded w-52" />
      <div className="h-3 bg-gray-100 rounded w-full max-w-md" />
      <SkeletonFormGrid fields={3} />
    </div>
  );
}

export interface FundingCompanyStepProps {
  lienId: string;
}

// Step 2 — always edits an existing lien (the route requires an id; a lien
// is only created by completing step 1 first).
export default function FundingCompanyStep({
  lienId,
}: FundingCompanyStepProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const isDetailEdit = searchParams.get(DETAIL_EDIT_PARAM) === DETAIL_EDIT_VALUE;
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
        setFormData(buildFormsFromLien(lien).providerFunding);
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
      await liensService.saveProviderFundingDetails(lienId, {
        medicalProviderId: formData?.medicalProviderId || undefined,
        facilityId: formData?.facilityId || undefined,
        fundingCompanyId: formData?.fundingCompanyId || null,
        fundingCompanyContactId: formData?.fundingCompanyContactId || null,
      });
      if (isDetailEdit) {
        toast.success("Provider & funding details updated.");
        router.push(detailHref(lienId));
        return;
      }
      goToStep(router, lienId, 3);
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
      step={2}
      hydrating={hydrating}
      skeleton={<FundingCompanyStepSkeleton />}
      submitting={submitting}
      continueDisabled={!formValid}
      onBack={
        isDetailEdit
          ? () => router.push(detailHref(lienId))
          : () => goToStep(router, lienId, 1)
      }
      onContinue={handleContinue}
      detailEditReturnHref={isDetailEdit ? detailHref(lienId) : undefined}
    >
      <FundingCompanyInfo
        lienId={lienId}
        data={formData}
        onFormValid={onFormValid}
      />
    </LienWizardShell>
  );
}
