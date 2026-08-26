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
  submitting?: boolean;
  onContinue: (data: CaseInfoFieldsValue) => void;
  onBack: () => void;
}

// Step 1 of the case-creation wizard. Mirrors LienInfoStep's shape
// (@/components/selling/lien-wizard/lien-info-step). onContinue creates the
// case draft (POST /case-drafts) before advancing to step 2 — submitting
// reflects that request's pending state.
export default function CaseInfoStep({
  data,
  submitting,
  onContinue,
  onBack,
}: CaseInfoStepProps) {
  const [form, setForm] = useState<CaseInfoFieldsValue>(data);
  const [valid, setValid] = useState(false);

  useEffect(() => {
    setValid(!!form.accidentTypeId && !!form.accidentStateId && !!form.lawfirmId);
  }, [form]);

  return (
    <CaseWizardShell
      step={1}
      totalSteps={TOTAL_STEPS}
      continueDisabled={!valid}
      submitting={submitting}
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
