"use client";

import { useEffect, useState } from "react";
import {
  PlaintiffInfoFields,
  PLAINTIFF_INFO_INITIAL_FORM,
  type PlaintiffInfoFieldsValue,
} from "../forms/add-case/plaintiff-info-fields";
import { CaseWizardShell } from "./shell";
import { TOTAL_STEPS } from "./shared";
import { isValidPhone } from "@/lib/phone";

export interface PlaintiffInfoStepProps {
  data: PlaintiffInfoFieldsValue;
  submitting?: boolean;
  onContinue: (data: PlaintiffInfoFieldsValue) => void;
  onBack: () => void;
}

// Step 2 of the case-creation wizard — see CaseInfoStep for the wizard's
// overall shape/rationale.
export default function PlaintiffInfoStep({
  data,
  submitting,
  onContinue,
  onBack,
}: PlaintiffInfoStepProps) {
  const [form, setForm] = useState<PlaintiffInfoFieldsValue>(data);
  const [valid, setValid] = useState(false);

  useEffect(() => {
    const phoneValid = !form.phone || isValidPhone(form.phone);
    setValid(
      !!form.firstName && !!form.lastName && !!form.birthdate && phoneValid,
    );
  }, [form]);

  return (
    <CaseWizardShell
      step={2}
      totalSteps={TOTAL_STEPS}
      continueDisabled={!valid}
      submitting={submitting}
      continueLabel="Add Case"
      onBack={onBack}
      onContinue={() => onContinue(form)}
    >
      <div className="mb-4">
        <h2 className="text-2xl font-semibold">Plaintiff Information</h2>
        <p className="text-sm text-gray-600 mt-1">
          Provide the plaintiff&apos;s personal and contact information to
          complete the case.
        </p>
      </div>
      <PlaintiffInfoFields
        value={form}
        onChange={(patch) => setForm((prev) => ({ ...prev, ...patch }))}
      />
    </CaseWizardShell>
  );
}

export { PLAINTIFF_INFO_INITIAL_FORM };
