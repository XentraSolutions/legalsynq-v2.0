"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  PlaintiffInfoFields,
  PLAINTIFF_INFO_INITIAL_FORM,
  type PlaintiffInfoFieldsValue,
} from "@/components/selling/forms/add-case/plaintiff-info-fields";
import {
  useSellingCaseDetail,
  useUpdateCasePlaintiff,
  SELLING_CASE_QUERY_KEY,
} from "@/hooks/selling/use-case-drafts";
import { EditPageShell } from "../edit-page-shell";

export default function EditPlaintiffInfoPage() {
  const params = useParams<{ id: string }>();
  const caseId = params?.id ?? "";
  const router = useRouter();
  const queryClient = useQueryClient();
  const detailPath = `/selling/portfolio/cases/${caseId}`;

  const { data: detail } = useSellingCaseDetail(caseId);
  const updatePlaintiff = useUpdateCasePlaintiff();

  const [form, setForm] = useState<PlaintiffInfoFieldsValue>(
    PLAINTIFF_INFO_INITIAL_FORM,
  );

  useEffect(() => {
    if (!detail) return;
    setForm({
      firstName: detail.firstName ?? "",
      lastName: detail.lastName ?? "",
      birthdate: detail.birthdate ?? "",
      email: detail.email ?? "",
      phone: detail.phone ?? "",
      sex: detail.gender ?? "",
      address: detail.address ?? "",
      city: detail.city ?? "",
      state: detail.state ?? "",
      zipcode: detail.zipcode ?? "",
    });
  }, [detail]);

  const handleSave = () => {
    updatePlaintiff.mutate(
      {
        caseId,
        request: {
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
        },
      },
      {
        onSuccess: () => {
          toast.success("Plaintiff information updated");
          queryClient.invalidateQueries({ queryKey: SELLING_CASE_QUERY_KEY(caseId) });
          router.push(detailPath);
        },
        onError: () => {
          toast.error("Failed to update plaintiff information");
        },
      },
    );
  };

  return (
    <EditPageShell
      backHref={detailPath}
      title="Plaintiff Information"
      subtitle="Review and update the plaintiff information as needed."
      onCancel={() => router.push(detailPath)}
      onSave={handleSave}
      saving={updatePlaintiff.isPending}
    >
      <PlaintiffInfoFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </EditPageShell>
  );
}
