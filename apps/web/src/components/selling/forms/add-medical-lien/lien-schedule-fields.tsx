import Field from "@/components/lien/field";

export interface LienScheduleFieldsValue {
  initialServiceDate: string;
  endServiceDate: string;
  listingVisibility: string;
  notes: string;
}

// Listing visibility has no UI control — every lien is created/edited as
// "Private" (matches the backend's ListingVisibility casing).
export const DEFAULT_LISTING_VISIBILITY = "Private";

// Shared by LienInfo (add/edit step-1) and EditLienInformationModal (lien
// detail page) — the schedule/notes fields both capture for a lien.
// LienInfo additionally renders its own "Lien Status" field above this,
// since only the wizard step edits status.
export function LienScheduleFields({
  value,
  onChange,
  requireInitialServiceDate = false,
}: {
  value: LienScheduleFieldsValue;
  onChange: (patch: Partial<LienScheduleFieldsValue>) => void;
  requireInitialServiceDate?: boolean;
}) {
  return (
    <>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
        <Field
          type="date"
          required={requireInitialServiceDate}
          label="Initial Service Date"
          value={value.initialServiceDate}
          onChange={(v) => onChange({ initialServiceDate: v })}
        />
        <Field
          type="date"
          label="End Service Date"
          value={value.endServiceDate}
          onChange={(v) => onChange({ endServiceDate: v })}
        />
      </div>
      <div className="grid grid-cols-1 gap-4 mt-4">
        <Field
          type="textarea"
          label="Lien Notes"
          value={value.notes}
          onChange={(v) => onChange({ notes: v })}
        />
      </div>
    </>
  );
}
