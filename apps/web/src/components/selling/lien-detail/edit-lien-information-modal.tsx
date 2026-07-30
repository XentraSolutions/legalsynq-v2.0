"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
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
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Initial Service Date
            </label>
            <input
              type="date"
              value={initialServiceDate}
              onChange={(e) => setInitialServiceDate(e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              End Service Date
            </label>
            <input
              type="date"
              value={endServiceDate}
              onChange={(e) => setEndServiceDate(e.target.value)}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Listing Visibility
          </label>
          <select
            value={listingVisibility}
            onChange={(e) => setListingVisibility(e.target.value)}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
          >
            <option value="Private">Private</option>
            <option value="Public">Public</option>
          </select>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Lien Notes
          </label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
          />
        </div>
      </div>
    </FormModal>
  );
}
