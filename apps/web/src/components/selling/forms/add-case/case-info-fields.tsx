import Field from "@/components/lien/field";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { useSessionContext } from "@/providers/session-provider";

export interface CaseInfoFieldsValue {
  caseStatusId: string;
  accidentTypeId: string;
  accidentStateId: string;
  dateOfLoss: string;
  lawfirmId: string;
  caseManagerId: string;
  notes: string;
}

export const CASE_INFO_INITIAL_FORM: CaseInfoFieldsValue = {
  caseStatusId: "",
  accidentTypeId: "",
  accidentStateId: "",
  dateOfLoss: "",
  lawfirmId: "",
  caseManagerId: "",
  notes: "",
};

// Case wizard step 1 — moves Law Firm/Case Manager off the lien wizard onto
// the case itself, where they belong (see the "New Case Added" continue
// flow's caseId hand-off back into Add Lien).
export function CaseInfoFields({
  value,
  onChange,
}: {
  value: CaseInfoFieldsValue;
  onChange: (patch: Partial<CaseInfoFieldsValue>) => void;
}) {
  const { lookup } = useSessionContext();

  // POST /case-drafts expects caseStatus/accidentState as lookup `code`
  // (e.g. "PreDemand", "CA") but accidentTypeId as the lookup `id` GUID —
  // confirmed against the live endpoint's validation errors, which reject a
  // code for accidentTypeId ("must identify an active accident type") and
  // reject a GUID for handlingLawFirmId's sibling status/state fields.
  const statusList =
    lookup?.CaseStatus.map((c) => ({ key: c.id, value: c.code, label: c.name })) ?? [];
  const accidentTypeList =
    lookup?.AccidentType.map((c) => ({ key: c.id, value: c.id, label: c.name })) ?? [];
  const stateList =
    lookup?.State.map((c) => ({ key: c.id, value: c.code, label: c.name })) ?? [];

  return (
    <div className="grid grid-cols-2 gap-4">
      <Field
        required
        label="Status"
        type="select"
        value={value.caseStatusId}
        options={statusList}
        placeholder="Select case status"
        onChange={(v: string) => onChange({ caseStatusId: v.toString() })}
      />
      <Field
        required
        label="Accident Type"
        type="select"
        value={value.accidentTypeId}
        options={accidentTypeList}
        placeholder="Select accident type"
        onChange={(v: string) => onChange({ accidentTypeId: v.toString() })}
      />
      <Field
        required
        label="Accident State"
        type="select"
        value={value.accidentStateId}
        options={stateList}
        placeholder="Select accident state"
        onChange={(v: string) => onChange({ accidentStateId: v.toString() })}
      />
      <Field
        label="Date of Loss"
        type="date"
        value={value.dateOfLoss}
        maxDate={new Date()}
        onChange={(v) => onChange({ dateOfLoss: v.toString() })}
      />
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Law Firm<span className="text-red-500 ml-0.5">*</span>
        </label>
        <SellingEntitySelect
          entityType="LawFirm"
          value={value.lawfirmId}
          onChange={(v) => onChange({ lawfirmId: v, caseManagerId: "" })}
          placeholder="Select law firm..."
          searchPlaceholder="Search law firms..."
          allowCreate
          createLabel="Add New Law Firm"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Case Manager
        </label>
        <SellingEntitySelect
          entityType="LawFirm"
          contactType="CaseManager"
          companyId={value.lawfirmId}
          isContactPerson
          requireParent
          parentHint="Select a law firm first"
          value={value.caseManagerId}
          onChange={(v) => onChange({ caseManagerId: v })}
          placeholder="Select case manager..."
          searchPlaceholder="Search case managers..."
          allowCreate
          createLabel="Add Case Manager"
        />
      </div>
      <div className="col-span-2">
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Case Tracking Notes
        </label>
        <textarea
          value={value.notes}
          onChange={(e) => onChange({ notes: e.target.value })}
          placeholder="Leave case tracking notes here..."
          rows={3}
          className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
        />
      </div>
    </div>
  );
}
