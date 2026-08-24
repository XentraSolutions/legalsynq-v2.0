"use client";

import { useEffect, useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { BaseSelect } from "@/components/ui/base-select";
import { Button } from "@/components/selling/button";

interface FilterOption {
  value: string;
  label: string;
}

export interface ContactsFilterField {
  key: string;
  label: string;
  options: FilterOption[];
}

interface ContactsFilterModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  fields: ContactsFilterField[];
  value: Record<string, string>;
  onApply: (value: Record<string, string>) => void;
  /**
   * Fires with the in-progress draft on every field change (and whenever the
   * draft resets to `value`, e.g. on open/cancel) — lets a caller with
   * dependent fields (e.g. Role scoped to the currently-picked Company Type)
   * react to a selection before Apply is clicked, since `value` itself only
   * updates on submit.
   */
  onDraftChange?: (draft: Record<string, string>) => void;
}

/**
 * Filter modal for the Contact Person directory (two dependent criteria —
 * Company Type then Role — so a modal groups them; a single-criterion list
 * like Companies just shows its dropdown inline in the toolbar instead, see
 * CompaniesListView).
 */
export function ContactsFilterModal({
  open,
  onClose,
  title = "Filter",
  fields,
  value,
  onApply,
  onDraftChange,
}: ContactsFilterModalProps) {
  const [draft, setDraft] = useState<Record<string, string>>(value);

  // Keyed on `open` alone, not `value` — `value` is a fresh object literal
  // from the caller on every render (see contact-persons-directory-view.tsx),
  // so keying on it too would reset `draft` back to the last-applied filters
  // on every re-render while the modal is open, wiping out any in-progress
  // (not-yet-applied) selection — e.g. as soon as `onDraftChange` below
  // causes the caller to re-render in response to a pick.
  useEffect(() => {
    if (open) setDraft(value);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    onDraftChange?.(draft);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft]);

  const handleSubmit = () => {
    onApply(draft);
    onClose();
  };

  const handleClear = () => {
    setDraft(Object.fromEntries(fields.map((f) => [f.key, ""])));
  };

  const handleClose = () => {
    setDraft(value);
    onClose();
  };

  return (
    <FormModal
      open={open}
      onClose={handleClose}
      onSubmit={handleSubmit}
      title={title}
      subtitle="Narrow down results using filters to quickly find relevant records."
      submitLabel="Apply Filters"
      size="md"
    >
      <div className="mb-4 flex justify-end">
        <Button
          type="button"
          variant="secondary"
          leftIcon="refreshCw"
          className="self-baseline-last"
          onClick={handleClear}
        >
          Clear Filter
        </Button>
      </div>
      <div className="gap-4 grid grid-cols-2">
        {fields.map((field) => (
          <div className="col-span-1" key={field.key}>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              {field.label}
            </label>
            <BaseSelect
              value={draft[field.key] ?? ""}
              onChange={(v) => setDraft({ ...draft, [field.key]: v as string })}
              options={field.options}
              placeholder={`All ${field.label}`}
              clearable
            />
          </div>
        ))}
      </div>
    </FormModal>
  );
}
