"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import {
  PlaintiffInfoFields,
  PLAINTIFF_INFO_INITIAL_FORM,
  type PlaintiffInfoFieldsValue,
} from "../forms/add-case/plaintiff-info-fields";
import { CaseWizardShell } from "./shell";
import {
  detailHref,
  draftStepHref,
  DETAIL_EDIT_PARAM,
  DETAIL_EDIT_VALUE,
} from "./shared";
import { NewCaseAddedModal } from "./new-case-added-modal";
import { isValidPhone } from "@/lib/phone";
import {
  useFinalizeCaseDraft,
  useUpdateCasePlaintiff,
  useSellingCaseDetail,
} from "@/hooks/selling/use-case-drafts";
import type { FinalizeCaseDraftRequest } from "@/lib/selling";

export interface PlaintiffInfoStepProps {
  // Finishing an in-progress draft (route: /cases/draft/[draftId]/step-2) —
  // continuing here finalizes the draft into a real case.
  draftId?: string;
  // An existing, already-finalized case being edited from its detail page
  // (route: /cases/[id]/edit/step-2). Always implies a standalone
  // Cancel/Save edit.
  caseId?: string;
}

function buildRequest(form: PlaintiffInfoFieldsValue): FinalizeCaseDraftRequest {
  return {
    firstName: form.firstName,
    lastName: form.lastName,
    ...(form.birthdate && { birthdate: form.birthdate }),
    email: form.email || undefined,
    phone: form.phone || undefined,
    gender: form.sex || undefined,
    address: form.address || undefined,
    city: form.city || undefined,
    state: form.state || undefined,
    zipcode: form.zipcode || undefined,
  };
}

// Step 2 — always finishes a draft (/draft/[draftId]/step-2) or edits an
// existing case's plaintiff info (/[id]/edit/step-2, always a standalone
// edit). Mirrors FundingCompanyStep's shape
// (@/components/selling/lien-wizard/funding-company-step).
export default function PlaintiffInfoStep({
  draftId,
  caseId,
}: PlaintiffInfoStepProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const isDetailEdit =
    !!caseId && searchParams.get(DETAIL_EDIT_PARAM) === DETAIL_EDIT_VALUE;
  const [form, setForm] = useState<PlaintiffInfoFieldsValue>(
    PLAINTIFF_INFO_INITIAL_FORM,
  );
  const [created, setCreated] = useState<{ caseId: string; caseNumber: string } | null>(
    null,
  );

  const { data: caseDetail, isLoading: caseLoading } = useSellingCaseDetail(
    caseId,
    { enabled: !!caseId },
  );
  const finalizeCaseDraft = useFinalizeCaseDraft();
  const updateCasePlaintiff = useUpdateCasePlaintiff();

  useEffect(() => {
    if (!caseDetail) return;
    setForm({
      firstName: caseDetail.firstName ?? "",
      lastName: caseDetail.lastName ?? "",
      birthdate: caseDetail.birthdate ?? "",
      email: caseDetail.email ?? "",
      phone: caseDetail.phone ?? "",
      sex: caseDetail.gender ?? "",
      address: caseDetail.address ?? "",
      city: caseDetail.city ?? "",
      state: caseDetail.state ?? "",
      zipcode: caseDetail.zipcode ?? "",
    });
  }, [caseDetail]);

  const phoneValid = !form.phone || isValidPhone(form.phone);
  const valid = !!form.firstName && !!form.lastName && !!form.birthdate && phoneValid;
  const submitting = finalizeCaseDraft.isPending || updateCasePlaintiff.isPending;

  const handleContinue = async () => {
    const request = buildRequest(form);
    try {
      if (caseId) {
        await updateCasePlaintiff.mutateAsync({ caseId, request });
        toast.success("Plaintiff information updated.");
        router.push(detailHref(caseId));
        return;
      }

      if (!draftId) return;
      const finalized = await finalizeCaseDraft.mutateAsync({ draftId, request });
      setCreated({ caseId: finalized.caseId, caseNumber: finalized.caseNumber });
    } catch {
      toast.error(
        caseId ? "Failed to update plaintiff information" : "Failed to create case",
      );
    }
  };

  return (
    <>
      <CaseWizardShell
        step={2}
        hydrating={!!caseId && caseLoading}
        submitting={submitting}
        continueDisabled={!valid}
        continueLabel={caseId ? "Continue" : "Add Case"}
        onBack={
          isDetailEdit && caseId
            ? () => router.push(detailHref(caseId))
            : () => router.push(draftStepHref(draftId ?? "", 1))
        }
        onContinue={handleContinue}
        detailEditReturnHref={isDetailEdit && caseId ? detailHref(caseId) : undefined}
      >
        <div className="mb-4">
          <h2 className="text-2xl font-semibold">Plaintiff Information</h2>
          <p className="text-sm text-gray-600 mt-1">
            Provide the plaintiff&apos;s personal and contact information to
            complete the case.
          </p>
        </div>
        <PlaintiffInfoFields
          value={form}
          onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
        />
      </CaseWizardShell>
      <NewCaseAddedModal
        open={!!created}
        caseNumber={created?.caseNumber}
        onClose={() => router.push("/selling/portfolio/cases")}
        onAddLien={() =>
          router.push(`/selling/portfolio/lien/add?caseId=${created?.caseId}`)
        }
      />
    </>
  );
}
