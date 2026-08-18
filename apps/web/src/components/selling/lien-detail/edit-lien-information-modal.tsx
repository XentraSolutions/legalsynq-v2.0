"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
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
      toast.success("Lien information updated.");
      onSaved();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save lien information");
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
