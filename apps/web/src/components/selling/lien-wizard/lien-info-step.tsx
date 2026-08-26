"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { LienInfoParams } from "@/lib/liens/liens.types";
import { toast } from "sonner";
import LienInfo from "../forms/add-medical-lien/lien-info";
import { LienWizardShell } from "./shell";
import { buildFormsFromLien } from "./shared";
import { SkeletonField, SkeletonFormGrid } from "@/components/lien/skeleton-loader";
import {
  CaseIntakeForm,
  PlaintiffIntakeForm,
} from "./case-intake-form";
import type {
  CreateSellingCaseDraftRequest,
  FinalizeSellingCaseDraftPlaintiffRequest,
} from "@/lib/selling/liens.types";

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
  // Existing lien being edited (route: edit/step-1). The /add route creates
  // a case draft and plaintiff first, then creates the lien with that case.
  lienId?: string;
  caseId?: string;
}

// /add is a two-step case intake; edit/step-1 updates lien information.
export default function LienInfoStep({ lienId, caseId }: LienInfoStepProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const draftId = searchParams.get("draftId");
  const [hydrating, setHydrating] = useState(!!lienId);
  const [submitting, setSubmitting] = useState(false);
  const [formData, setFormData] = useState<any>(null);
  const [formValid, setFormValid] = useState(false);

  const [caseDraft, setCaseDraft] =
    useState<CreateSellingCaseDraftRequest | null>(null);
  const [plaintiff, setPlaintiff] =
    useState<FinalizeSellingCaseDraftPlaintiffRequest | null>(null);

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
    if (!lienId) {
      if (!draftId) {
        if (!caseDraft) return;
        setSubmitting(true);
        try {
          const draft = await liensService.createCaseDraft({
            accidentTypeId: caseDraft.accidentTypeId || undefined,
            accidentState: caseDraft.accidentState || undefined,
            dateOfLoss: caseDraft.dateOfLoss || undefined,
            handlingLawFirmId: caseDraft.handlingLawFirmId || undefined,
            caseManagerId: caseDraft.caseManagerId || undefined,
            caseTrackingNotes: caseDraft.caseTrackingNotes || undefined,
          });
          router.push(
            `/selling/portfolio/lien/add?draftId=${encodeURIComponent(draft.draftId)}`,
          );
        } catch (err) {
          toast.error(err instanceof Error ? err.message : "Failed to create case draft");
        } finally {
          setSubmitting(false);
        }
        return;
      }

      if (!plaintiff) return;
      setSubmitting(true);
      try {
        const finalized = await liensService.finalizeCaseDraft(draftId, {
          firstName: plaintiff.firstName,
          lastName: plaintiff.lastName,
          birthdate: plaintiff.birthdate || undefined,
          email: plaintiff.email || undefined,
          phone: plaintiff.phone || undefined,
          gender: plaintiff.gender || undefined,
          address: plaintiff.address || undefined,
          city: plaintiff.city || undefined,
          state: plaintiff.state || undefined,
          zipcode: plaintiff.zipcode || undefined,
        });
        const created = await liensService.createLien({
          caseId: finalized.caseId,
          sellerStatus: "Pending",
          source: "Single",
        });
        toast.success("Case created");
        router.push(`/selling/portfolio/lien/${created.lienId}/edit/step-1`);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to create case");
      } finally {
        setSubmitting(false);
      }
      return;
    }

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

      await liensService.createLienInfo(lienId, request);
      router.push(`/selling/portfolio/lien/${lienId}/edit/step-2`);

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
      step={!lienId && draftId ? 2 : 1}
      hydrating={hydrating}
      skeleton={<LienInfoSkeleton />}
      submitting={submitting}
      continueDisabled={!formValid}
      onBack={() => router.back()}
      onContinue={handleContinue}
    >
      {!lienId && !draftId ? (
        <CaseIntakeForm
          onFormValid={(valid, data) => {
            setFormValid(valid);
            setCaseDraft(data);
          }}
        />
      ) : !lienId ? (
        <PlaintiffIntakeForm
          onFormValid={(valid, data) => {
            setFormValid(valid);
            setPlaintiff(data);
          }}
        />
      ) : (
        <LienInfo
          caseId={caseId}
          lienId={lienId}
          data={formData}
          onFormValid={onFormValid}
        />
      )}
    </LienWizardShell>
  );
}
