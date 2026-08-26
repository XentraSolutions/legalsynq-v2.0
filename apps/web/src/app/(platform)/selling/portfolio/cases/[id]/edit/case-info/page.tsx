"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  CaseInfoFields,
  CASE_INFO_INITIAL_FORM,
  type CaseInfoFieldsValue,
} from "@/components/selling/forms/add-case/case-info-fields";
import {
  useSellingCaseDetail,
  useUpdateCase,
  SELLING_CASE_QUERY_KEY,
} from "@/hooks/selling/use-case-drafts";
import { EditPageShell } from "../edit-page-shell";

export default function EditCaseInfoPage() {
  const params = useParams<{ id: string }>();
  const caseId = params?.id ?? "";
  const router = useRouter();
  const queryClient = useQueryClient();
  const detailPath = `/selling/portfolio/cases/${caseId}`;

  const { data: detail } = useSellingCaseDetail(caseId);
  const updateCase = useUpdateCase();

  const [form, setForm] = useState<CaseInfoFieldsValue>(CASE_INFO_INITIAL_FORM);

  useEffect(() => {
    if (!detail) return;
    setForm({
      accidentTypeId: detail.accidentTypeId ?? "",
      accidentStateId: detail.accidentState ?? "",
      dateOfLoss: detail.dateOfLoss ?? "",
      lawfirmId: detail.handlingLawFirmId ?? "",
      caseManagerId: detail.caseManagerId ?? "",
      notes: detail.caseTrackingNotes ?? "",
    });
  }, [detail]);

  const handleSave = () => {
    updateCase.mutate(
      {
        caseId,
        request: {
          accidentTypeId: form.accidentTypeId,
          accidentState: form.accidentStateId,
          handlingLawFirmId: form.lawfirmId,
          // Omit rather than send "" — the backend's GUID/date model
          // binders 400 on an empty string instead of treating it as
          // absent (same convention as case-wizard.tsx).
          ...(form.dateOfLoss && { dateOfLoss: form.dateOfLoss }),
          ...(form.caseManagerId && { caseManagerId: form.caseManagerId }),
          ...(form.notes && { caseTrackingNotes: form.notes }),
        },
      },
      {
        onSuccess: () => {
          toast.success("Case information updated");
          queryClient.invalidateQueries({ queryKey: SELLING_CASE_QUERY_KEY(caseId) });
          router.push(detailPath);
        },
        onError: () => {
          toast.error("Failed to update case information");
        },
      },
    );
  };

  return (
    <EditPageShell
      backHref={detailPath}
      title="Case Information"
      subtitle="Review and update the case information as needed."
      onCancel={() => router.push(detailPath)}
      onSave={handleSave}
      saving={updateCase.isPending}
    >
      <CaseInfoFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </EditPageShell>
  );
}
