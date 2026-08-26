"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
import type {
  LienFundingCompanyDetail,
  LienMedicalProviderDetail,
} from "@/types/lien-selling";
import {
  CaseInformationFields,
  type CaseInformationFieldsValue,
} from "@/components/selling/forms/add-medical-lien/case-information-fields";

interface EditCaseInformationModalProps {
  lienId: string;
  fundingCompany: LienFundingCompanyDetail | null;
  medicalProvider: LienMedicalProviderDetail | null;
  onClose: () => void;
  onSaved: () => void;
}

export function EditCaseInformationModal({
  lienId,
  fundingCompany,
  medicalProvider,
  onClose,
  onSaved,
}: EditCaseInformationModalProps) {
  const [form, setForm] = useState<CaseInformationFieldsValue>({
    medicalProviderId: medicalProvider?.id ?? "",
    fundingCompanyId: fundingCompany?.id ?? "",
    fundingCompanyContactId: fundingCompany?.contact?.id ?? "",
  });
  const [saving, setSaving] = useState(false);

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveCaseInformation(lienId, {
        medicalProviderId: form.medicalProviderId || undefined,
        fundingCompanyId: form.fundingCompanyId || undefined,
        fundingCompanyContactId: form.fundingCompanyContactId || undefined,
      });
      toast.success("Lien associations updated.");
      onSaved();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save case information");
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormModal
      open
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Edit Lien Associations"
      submitLabel={saving ? "Saving..." : "Save"}
      loading={saving}
    >
      <CaseInformationFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </FormModal>
  );
}
