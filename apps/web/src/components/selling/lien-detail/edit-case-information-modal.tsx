"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
import type {
  LienCaseDetail,
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
  caseInformation: LienCaseDetail | null;
  onClose: () => void;
  onSaved: () => void;
}

export function EditCaseInformationModal({
  lienId,
  fundingCompany,
  medicalProvider,
  caseInformation,
  onClose,
  onSaved,
}: EditCaseInformationModalProps) {
  const [form, setForm] = useState<CaseInformationFieldsValue>({
    medicalProviderId: medicalProvider?.id ?? "",
    fundingCompanyId: fundingCompany?.id ?? "",
    fundingCompanyContactId: fundingCompany?.contact?.id ?? "",
    lawfirmId: caseInformation?.lawFirmId ?? "",
    caseManagerId: caseInformation?.caseManagerId ?? "",
  });
  const [saving, setSaving] = useState(false);

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveCaseInformation(lienId, {
        medicalProviderId: form.medicalProviderId || undefined,
        fundingCompanyId: form.fundingCompanyId || undefined,
        fundingCompanyContactId: form.fundingCompanyContactId || undefined,
        handlingLawFirmId: form.lawfirmId || undefined,
        caseManagerId: form.caseManagerId || undefined,
        caseId: caseInformation?.id,
        createCaseIfMissing: !caseInformation?.id,
      });
      toast.success("Funding company, medical provider & case information updated.");
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
      title="Edit Case Information"
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
