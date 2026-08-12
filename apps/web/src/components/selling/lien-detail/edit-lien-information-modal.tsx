"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
import Field from "@/components/lien/field";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import type { LienDetail } from "@/types/lien-selling";

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
  const [initialServiceDate, setInitialServiceDate] = useState(
    lien.initialServiceDate ?? "",
  );
  const [endServiceDate, setEndServiceDate] = useState(
    lien.endServiceDate ?? "",
  );
  const [listingVisibility, setListingVisibility] = useState(
    lien.listingVisibility || "Private",
  );
  const [notes, setNotes] = useState(lien.notes ?? "");
  const [saving, setSaving] = useState(false);

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveLienInformation(lienId, {
        sellerStatus: lien.sellerStatus,
        initialServiceDate: initialServiceDate || undefined,
        endServiceDate: endServiceDate || undefined,
        listingVisibility,
        notes: notes || undefined,
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
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <Field
            type="date"
            label="Initial Service Date"
            value={initialServiceDate}
            onChange={setInitialServiceDate}
          />
          <Field
            type="date"
            label="End Service Date"
            value={endServiceDate}
            onChange={setEndServiceDate}
          />
        </div>
        <Field
          type="select"
          label="Listing Visibility"
          value={listingVisibility}
          onChange={setListingVisibility}
          options={[
            { key: "Private", value: "Private", label: "Private" },
            { key: "Public", value: "Public", label: "Public" },
          ]}
        />
        <Field
          type="textarea"
          label="Lien Notes"
          value={notes}
          onChange={setNotes}
        />
      </div>
    </FormModal>
  );
}
