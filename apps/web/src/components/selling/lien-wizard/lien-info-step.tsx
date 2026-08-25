"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { LienInfoParams } from "@/lib/liens/liens.types";
import { toast } from "sonner";
import LienInfo from "../forms/add-medical-lien/lien-info";
import { LienWizardShell } from "./shell";
import { buildFormsFromLien } from "./shared";
import { SkeletonField, SkeletonFormGrid } from "@/components/lien/skeleton-loader";

// Mirrors LienInfo's layout: title, a 2x2 field grid (status/date,
// date/select), then a full-width notes textarea.
function LienInfoSkeleton() {
  return (
    <div className="space-y-4 animate-pulse pb-3">
      <div className="h-6 bg-gray-100 rounded w-44" />
      <SkeletonFormGrid fields={4} />
      <SkeletonField full />
    </div>
  );
}

export interface LienInfoStepProps {
  // Existing lien being edited (route: edit/step-1). Omitted on the
  // brand-new /add page, where step 1 doubles as lien creation — the case
  // itself is picked via CaseSelect (or created via the separate
  // @/components/selling/case-wizard flow) before this step ever submits.
  lienId?: string;
  caseId?: string;
}

// Step 1 — used both to create a brand-new lien (/add, no lienId) and to
// edit an existing one's info (/edit/step-1, lienId from the route).
export default function LienInfoStep({ lienId, caseId }: LienInfoStepProps) {
  const router = useRouter();
  const [hydrating, setHydrating] = useState(!!lienId);
  const [submitting, setSubmitting] = useState(false);
  const [formData, setFormData] = useState<any>(null);
  const [formValid, setFormValid] = useState(false);

  useEffect(() => {
    if (!lienId) return;
    let cancelled = false;
    (async () => {
      try {
        const lien = await liensService.getLienById(lienId);
        if (cancelled) return;
        setFormData(buildFormsFromLien(lien).lienInfo);
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
    if (!formData) return;
    setSubmitting(true);
    try {
      const request: LienInfoParams = {
        sellerStatus: formData.status,
        initialServiceDate: formData.initialServiceDate,
        endServiceDate: formData.endServiceDate || null,
        listingVisibility: formData.listingVisibility,
        notes: formData.notes,
      };

      // The lien only gets created once. Re-submitting step 1 after coming
      // back to it (via /edit/step-1) must update that same lien's info,
      // not POST /liens again. Note: the case a lien belongs to can only be
      // set at creation (CreateLienParams.caseId is required) — there's no
      // endpoint to reassign it afterward, so CaseSelect here is effectively
      // read-only once the lien exists.
      if (lienId) {
        await liensService.createLienInfo(lienId, request);
        router.push(`/selling/portfolio/lien/${lienId}/edit/step-2`);
        return;
      }

      if (!formData.caseId) return;
      const created = await liensService.createLien({
        caseId: formData.caseId,
        sellerStatus: request.sellerStatus,
        source: "Single",
      });
      await liensService.createLienInfo(created.lienId, request);
      toast.success("Liens Created");
      // Move the URL onto the resumable draft route so a refresh (or the
      // back button) continues editing this lien instead of creating another
      // bare one. The backend has no draft-listing endpoint yet, so this URL
      // is the only way progress survives a refresh.
      router.push(`/selling/portfolio/lien/${created.lienId}/edit/step-2`);
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
      step={1}
      hydrating={hydrating}
      skeleton={<LienInfoSkeleton />}
      submitting={submitting}
      continueDisabled={!formValid}
      onBack={() => router.back()}
      onContinue={handleContinue}
    >
      <LienInfo
        caseId={caseId}
        lienId={lienId}
        data={formData}
        onFormValid={onFormValid}
      />
    </LienWizardShell>
  );
}
