"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import type { LienDetail } from "@/types/lien-selling";
import {
  LienScheduleFields,
  type LienScheduleFieldsValue,
} from "@/components/selling/forms/add-medical-lien/lien-schedule-fields";

interface EditLienInformationModalProps {
  lienId: string;
  lien: LienDetail;
  onClose: () => void;
  onSaved: () => void;
}

export function EditLienInformationModal({
  lienId,
  lien,
  onClose,
  onSaved,
}: EditLienInformationModalProps) {
  const { show: showToast } = useToast();
  const [form, setForm] = useState<LienScheduleFieldsValue>({
    initialServiceDate: lien.initialServiceDate ?? "",
    endServiceDate: lien.endServiceDate ?? "",
    listingVisibility: lien.listingVisibility || "Private",
    notes: lien.notes ?? "",
  });
  const [saving, setSaving] = useState(false);

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveLienInformation(lienId, {
        sellerStatus: lien.sellerStatus,
        initialServiceDate: form.initialServiceDate || undefined,
        endServiceDate: form.endServiceDate || undefined,
        listingVisibility: form.listingVisibility,
        notes: form.notes || undefined,
      });
      showToast("Lien information updated.", "success");
      onSaved();
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to save lien information",
        "error",
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormModal
      open
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Edit Lien Information"
      submitLabel={saving ? "Saving..." : "Save"}
      loading={saving}
    >
      <LienScheduleFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </FormModal>
  );
}
