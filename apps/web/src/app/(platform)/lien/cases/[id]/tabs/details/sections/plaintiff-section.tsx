import Field from "@/components/lien/field";
import type { CaseDetail } from "@/lib/cases";
import type { DropdownOption } from "@/lib/lookup/lookup.types";
import { CollapsibleSection } from "../../../components/collapsible-section";
import { FieldGrid, FieldItem } from "../../../components/field-grid";

export function PlaintiffSection({
  d,
  canEdit,
  editing,
  onStartEdit,
  form,
  updateField,
  state,
  pPhone,
  setPPhone,
  pDob,
  setPDob,
  pSaving,
  onSave,
  onCancel,
}: {
  d: CaseDetail;
  canEdit: boolean;
  editing: boolean;
  onStartEdit: () => void;
  form: CaseDetail;
  updateField: (field: keyof CaseDetail, value: string) => void;
  state: DropdownOption[];
  pPhone: string;
  setPPhone: (value: string) => void;
  pDob: string;
  setPDob: (value: string) => void;
  pSaving: boolean;
  onSave: () => void;
  onCancel: () => void;
}) {
  return (
    <CollapsibleSection
      title="Plaintiff"
      icon="ri-user-line"
      onEdit={canEdit && !editing ? onStartEdit : undefined}
    >
      <div className="mb-3">
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">
          Plaintiff Info
        </p>
      </div>

      {editing ? (
        <div className="space-y-3">
          <div className="grid grid-cols-3 gap-x-8 gap-y-3 relative">
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                First Name *
              </label>
              <Field
                label=""
                value={form.clientFirstName}
                onChange={(e) => updateField("clientFirstName", e.toString())}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Last Name *
              </label>
              <Field
                label=""
                value={form.clientLastName}
                onChange={(e) => updateField("clientLastName", e.toString())}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Phone Number
              </label>
              <Field
                label=""
                type="tel"
                value={pPhone}
                onChange={(e) => {
                  setPPhone(e);
                  updateField("clientPhone", e.toString());
                }}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Email
              </label>
              <Field
                label=""
                value={form.clientEmail}
                onChange={(e) => updateField("clientEmail", e.toString())}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Date of Birth
              </label>
              <Field
                label=""
                type="date"
                value={pDob}
                onChange={(e) => {
                  setPDob(e.toString());
                  updateField("clientDob", e.toString());
                }}
                maxDate={new Date()}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">
                Sex
              </label>
              <Field
                label=""
                value={form.sex}
                type="select"
                options={[
                  { key: "male", value: "male", label: "Male" },
                  { key: "female", value: "female", label: "Female" },
                ]}
                onChange={(e: string) => updateField("sex", e.toString())}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Address
              </label>
              <Field
                label=""
                value={form.clientStreetAddress}
                onChange={(e) =>
                  updateField("clientStreetAddress", e.toString())
                }
              />
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                City
              </label>
              <Field
                label=""
                value={form.clientCity}
                onChange={(e) => updateField("clientCity", e.toString())}
              />
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                State
              </label>
              <Field
                label=""
                value={form.clientState}
                type="select"
                options={state}
                onChange={(e: string) => {
                  updateField("clientState", e.toString());
                }}
              />
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Zip code
              </label>
              <Field
                label=""
                value={form.clientZipcode}
                onChange={(e) => updateField("clientZipcode", e.toString())}
              />
            </div>
          </div>
          <div className="flex items-center gap-2 pt-1">
            <button
              onClick={onSave}
              disabled={pSaving}
              className="px-4 py-2 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-1.5 disabled:opacity-60"
            >
              {pSaving ? (
                <>
                  <i className="ri-loader-4-line text-sm animate-spin" />
                  Saving...
                </>
              ) : (
                <>
                  <i className="ri-save-line text-sm" />
                  Save
                </>
              )}
            </button>
            <button
              onClick={onCancel}
              disabled={pSaving}
              className="px-4 py-2 text-sm font-medium text-gray-500 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <FieldGrid>
          <FieldItem label="Full Name" value={d.clientName} />
          <FieldItem label="Phone Number" value={pPhone} />
          <FieldItem label="Email" value={d.clientEmail} />
          <FieldItem label="Date of Birth" value={d.clientDob} />
          <FieldItem label="Sex" value={d.sex} />
          <FieldItem label="Address" value={d.clientAddress} />
        </FieldGrid>
      )}
    </CollapsibleSection>
  );
}
