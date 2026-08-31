"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import {
  CaseInfoFields,
  CASE_INFO_INITIAL_FORM,
  type CaseInfoFieldsValue,
} from "../forms/add-case/case-info-fields";
import { CaseWizardShell } from "./shell";
import {
  detailHref,
  draftStepHref,
  caseEditStepHref,
  DETAIL_EDIT_PARAM,
  DETAIL_EDIT_VALUE,
} from "./shared";
import { SkeletonField, SkeletonFormGrid } from "@/components/lien/skeleton-loader";
import {
  useCreateCaseDraft,
  useUpdateCaseDraft,
  useUpdateCase,
  useCaseDraft,
  useSellingCaseDetail,
} from "@/hooks/selling/use-case-drafts";
import type { CaseDraftRequest } from "@/lib/selling";

// Mirrors CaseInfoFields' layout: a full-width field (Accident Type), a 2x2
// grid (Accident State/Date of Loss, Law Firm/Case Manager), then a
// full-width field (Case Tracking Notes). Same pattern as LienInfoStep's
// LienInfoSkeleton (@/components/selling/lien-wizard/lien-info-step).
function CaseInfoSkeleton() {
  return (
    <div className="space-y-4 animate-pulse pb-3">
      <div className="h-6 bg-gray-100 rounded w-44" />
      <SkeletonField full />
      <SkeletonFormGrid fields={4} />
      <SkeletonField full />
    </div>
  );
}

export interface CaseInfoStepProps {
  // An in-progress draft being resumed (route: /cases/draft/[draftId]/step-1).
  // Omitted on the brand-new /add page, where step 1 doubles as draft
  // creation.
  draftId?: string;
  // An existing, already-finalized case being edited (route:
  // /cases/[id]/edit/step-1). A "returnTo=detail" query param means this is
  // a standalone Cancel/Save edit of just this section from the case detail
  // page; without it, this is one step of the full multi-step edit wizard
  // (entered via /cases/[id]/edit) — see isDetailEdit below.
  caseId?: string;
}

function buildRequest(form: CaseInfoFieldsValue): CaseDraftRequest {
  return {
    accidentTypeId: form.accidentTypeId,
    accidentState: form.accidentStateId,
    handlingLawFirmId: form.lawfirmId,
    // Omit rather than send "" for these optional fields — the backend's
    // GUID/date model binders 400 on an empty string instead of treating it
    // as absent.
    ...(form.dateOfLoss && { dateOfLoss: form.dateOfLoss }),
    ...(form.caseManagerId && { caseManagerId: form.caseManagerId }),
    ...(form.notes && { caseTrackingNotes: form.notes }),
  };
}

// Step 1 — used to create a brand-new case draft (/add, no ids), to resume
// an in-progress draft (/draft/[draftId]/step-1), and to edit an existing
// case's info from its detail page (/[id]/edit/step-1). Mirrors
// LienInfoStep's shape (@/components/selling/lien-wizard/lien-info-step).
export default function CaseInfoStep({ draftId, caseId }: CaseInfoStepProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const isDetailEdit =
    !!caseId && searchParams.get(DETAIL_EDIT_PARAM) === DETAIL_EDIT_VALUE;
  const [form, setForm] = useState<CaseInfoFieldsValue>(CASE_INFO_INITIAL_FORM);

  const { data: draft, isLoading: draftLoading } = useCaseDraft(draftId, {
    enabled: !!draftId,
  });
  const { data: caseDetail, isLoading: caseLoading } = useSellingCaseDetail(
    caseId,
    { enabled: !!caseId },
  );
  const createCaseDraft = useCreateCaseDraft();
  const updateCaseDraft = useUpdateCaseDraft();
  const updateCase = useUpdateCase();

  const hydrating = (!!draftId && draftLoading) || (!!caseId && caseLoading);

  useEffect(() => {
    if (draft) {
      setForm({
        accidentTypeId: draft.accidentTypeId ?? "",
        accidentStateId: draft.accidentState ?? "",
        dateOfLoss: draft.dateOfLoss ?? "",
        lawfirmId: draft.handlingLawFirmId ?? "",
        caseManagerId: draft.caseManagerId ?? "",
        notes: draft.caseTrackingNotes ?? "",
      });
    }
  }, [draft]);

  useEffect(() => {
    if (caseDetail) {
      setForm({
        accidentTypeId: caseDetail.accidentTypeId ?? "",
        accidentStateId: caseDetail.accidentState ?? "",
        dateOfLoss: caseDetail.dateOfLoss ?? "",
        lawfirmId: caseDetail.handlingLawFirmId ?? "",
        caseManagerId: caseDetail.caseManagerId ?? "",
        notes: caseDetail.caseTrackingNotes ?? "",
      });
    }
  }, [caseDetail]);

  const valid =
    !!form.accidentTypeId && !!form.accidentStateId && !!form.lawfirmId;

  const submitting =
    createCaseDraft.isPending || updateCaseDraft.isPending || updateCase.isPending;

  const handleContinue = async () => {
    const request = buildRequest(form);
    try {
      if (caseId) {
        await updateCase.mutateAsync({ caseId, request });
        if (isDetailEdit) {
          toast.success("Case information updated.");
          router.push(detailHref(caseId));
          return;
        }
        router.push(caseEditStepHref(caseId, 2));
        return;
      }

      if (draftId) {
        await updateCaseDraft.mutateAsync({ draftId, request });
        router.push(draftStepHref(draftId, 2));
        return;
      }

      const created = await createCaseDraft.mutateAsync(request);
      // Swap /add's history entry for the resumable draft step-1 URL
      // *without* triggering a Next navigation (router.replace immediately
      // followed by router.push collapses into a single transition — the
      // replace never lands), then router.push step 2 as a normal
      // navigation on top of it. Back from step 2 now lands on step 1 with
      // this draft's id, not a blank /add that would spawn a second draft.
      window.history.replaceState(
        window.history.state,
        "",
        draftStepHref(created.draftId, 1),
      );
      router.push(draftStepHref(created.draftId, 2));
    } catch {
      toast.error(
        caseId
          ? "Failed to update case information"
          : "Failed to save case information",
      );
    }
  };

  return (
    <CaseWizardShell
      step={1}
      hydrating={hydrating}
      skeleton={<CaseInfoSkeleton />}
      submitting={submitting}
      continueDisabled={!valid}
      onBack={
        isDetailEdit && caseId
          ? () => router.push(detailHref(caseId))
          : caseId || draftId
            ? () => router.push("/selling/portfolio/cases")
            : () => router.back()
      }
      onCancel={
        isDetailEdit && caseId
          ? () => router.push(detailHref(caseId))
          : caseId || draftId
            ? () => router.push("/selling/portfolio/cases")
            : () => router.back()
      }
      onContinue={handleContinue}
      detailEditReturnHref={isDetailEdit && caseId ? detailHref(caseId) : undefined}
    >
      <div className="mb-4">
        <h2 className="text-2xl font-semibold">Case Information</h2>
        <p className="text-sm text-gray-600 mt-1">
          Provide the key information needed to create this case.
        </p>
      </div>
      <CaseInfoFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </CaseWizardShell>
  );
}
