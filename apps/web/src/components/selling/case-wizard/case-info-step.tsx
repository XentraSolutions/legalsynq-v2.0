"use client";

import { useEffect, useState } from "react";
import {
  CaseInfoFields,
  CASE_INFO_INITIAL_FORM,
  type CaseInfoFieldsValue,
} from "../forms/add-case/case-info-fields";
import { CaseWizardShell } from "./shell";
import { TOTAL_STEPS } from "./shared";

export interface CaseInfoStepProps {
  data: CaseInfoFieldsValue;
  onContinue: (data: CaseInfoFieldsValue) => void;
  onBack: () => void;
}

// Step 1 of the case-creation wizard. Mirrors LienInfoStep's shape
// (@/components/selling/lien-wizard/lien-info-step) but the whole wizard is
// client-state-only — there's no draft-id route to resume from, since case
// creation isn't wired to a real endpoint yet (stub submit in
// plaintiff-info-step.tsx).
export default function CaseInfoStep({
  data,
  onContinue,
  onBack,
}: CaseInfoStepProps) {
  const [form, setForm] = useState<CaseInfoFieldsValue>(data);
  const [valid, setValid] = useState(false);

  useEffect(() => {
    setValid(
      !!form.caseStatusId &&
        !!form.accidentTypeId &&
        !!form.accidentStateId &&
        !!form.lawfirmId,
    );
  }, [form]);

  return (
    <CaseWizardShell
      step={1}
      totalSteps={TOTAL_STEPS}
      continueDisabled={!valid}
      onBack={onBack}
      onContinue={() => onContinue(form)}
    >
      <div className="mb-4">
        <h2 className="text-2xl font-semibold">Case Information</h2>
        <p className="text-sm text-gray-600 mt-1">
          Provide the key information needed to create this case.
        </p>
      </div>
      <CaseInfoFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </CaseWizardShell>
  );
}

export { CASE_INFO_INITIAL_FORM };
