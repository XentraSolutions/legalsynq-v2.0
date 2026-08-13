import Field from "@/components/lien/field";

export interface LienScheduleFieldsValue {
  initialServiceDate: string;
  endServiceDate: string;
  listingVisibility: string;
  notes: string;
}

// Values must match the backend's ListingVisibility casing ("Public" /
// "Private") — a lowercase mismatch here means a hydrated lien's saved
// value never matches an option, showing the select as unset.
const LISTING_VISIBILITY_OPTIONS = [
  { key: "Public", value: "Public", label: "Public" },
  { key: "Private", value: "Private", label: "Private" },
];

// Shared by LienInfo (add/edit step-1) and EditLienInformationModal (lien
// detail page) — the schedule/visibility/notes fields both capture for a
// lien. LienInfo additionally renders its own "Lien Status" field above
// this, since only the wizard step edits status.
export function LienScheduleFields({
  value,
  onChange,
  requireInitialServiceDate = false,
  requireListingVisibility = false,
}: {
  value: LienScheduleFieldsValue;
  onChange: (patch: Partial<LienScheduleFieldsValue>) => void;
  requireInitialServiceDate?: boolean;
  requireListingVisibility?: boolean;
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
          type="select"
          options={LISTING_VISIBILITY_OPTIONS}
          required={requireListingVisibility}
          label="Listing Visibility"
          value={value.listingVisibility}
          onChange={(v: string) => onChange({ listingVisibility: v })}
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
