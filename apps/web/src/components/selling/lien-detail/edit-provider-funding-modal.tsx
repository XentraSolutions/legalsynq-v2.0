"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
import type {
  LienFacilityDetail,
  LienFundingCompanyDetail,
  LienMedicalProviderDetail,
} from "@/types/lien-selling";
import {
  ProviderFundingFields,
  type ProviderFundingFieldsValue,
} from "@/components/selling/forms/add-medical-lien/provider-funding-fields";

interface EditProviderFundingModalProps {
  lienId: string;
  fundingCompany: LienFundingCompanyDetail | null;
  medicalProvider: LienMedicalProviderDetail | null;
  facility?: LienFacilityDetail | null;
  onClose: () => void;
  onSaved: () => void;
}

export function EditProviderFundingModal({
  lienId,
  fundingCompany,
  medicalProvider,
  facility,
  onClose,
  onSaved,
}: EditProviderFundingModalProps) {
  const [form, setForm] = useState<ProviderFundingFieldsValue>({
    medicalProviderId: medicalProvider?.id ?? "",
    facilityId: facility?.id ?? "",
    fundingCompanyId: fundingCompany?.id ?? "",
    fundingCompanyContactId: fundingCompany?.contact?.id ?? "",
  });
  const [saving, setSaving] = useState(false);

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveProviderFundingDetails(lienId, {
        medicalProviderId: form.medicalProviderId || undefined,
        facilityId: form.facilityId || undefined,
        fundingCompanyId: form.fundingCompanyId || undefined,
        fundingCompanyContactId: form.fundingCompanyContactId || undefined,
      });
      toast.success("Lien associations updated.");
      onSaved();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save provider & funding details");
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
      <ProviderFundingFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </FormModal>
  );
}
