"use client";

import { ArrowLeft } from "lucide-react";
import { Button } from "@/components/selling/button";
import { SkeletonFormGrid } from "@/components/lien/skeleton-loader";
import { TOTAL_STEPS } from "./shared";

function ProgressBar({ currentStep }: { currentStep: number }) {
  return (
    <div className="w-full mx-auto p-4">
      <div className="flex w-full gap-2">
        {Array.from({ length: TOTAL_STEPS }, (_, index) => {
          const isFilled = index < currentStep;
          return (
            <div
              key={index}
              className={`h-1 flex-1 rounded-full transition-all duration-300 ease-in-out ${
                isFilled ? "bg-[#EE7132]" : "bg-gray-200"
              }`}
            />
          );
        })}
      </div>
    </div>
  );
}

export interface CaseWizardShellProps {
  step: number;
  hydrating?: boolean;
  skeleton?: React.ReactNode;
  submitting?: boolean;
  continueDisabled?: boolean;
  continueLabel?: string;
  // Circled top-left arrow: goes to the previous step (or, on step 1 where
  // there is no previous step, behaves the same as onCancel).
  onBack: () => void;
  // Footer "Cancel" button: exits the wizard entirely, back to wherever the
  // user came from — the cases list, or the case detail page in
  // detail-edit mode.
  onCancel: () => void;
  onContinue: () => void;
  children: React.ReactNode;
  // When set, this step is being opened as a standalone edit from the case
  // detail page (via a "returnTo=detail" query param) rather than as part of
  // the create wizard: hides the step progress bar, and the footer reads
  // Save instead of Continue. Mirrors LienWizardShell's detailEditReturnHref
  // (@/components/selling/lien-wizard/shell).
  detailEditReturnHref?: string;
}

// Shared chrome for every case-wizard step page — same shape as
// LienWizardShell (@/components/selling/lien-wizard/shell), but with a fixed
// TOTAL_STEPS (2) since the case wizard's step count doesn't vary by caller.
export function CaseWizardShell({
  step,
  hydrating,
  skeleton,
  submitting,
  continueDisabled,
  continueLabel = "Continue",
  onBack,
  onCancel,
  onContinue,
  children,
  detailEditReturnHref,
}: CaseWizardShellProps) {
  const isDetailEdit = !!detailEditReturnHref;
  return (
    <div className="max-w-[700px] m-auto">
      <div className="flex items-center mb-6 ">
        <nav>
          <button
            type="button"
            onClick={onBack}
            className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors shrink-0"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
        </nav>
        {!isDetailEdit && <ProgressBar currentStep={step} />}
      </div>
      {!isDetailEdit && (
        <p className={`mt-2 text-xs text-gray-600`}>
          Step {step}/ {TOTAL_STEPS}
        </p>
      )}
      <div className="mt-5 position-relative ">
        {hydrating ? (
          skeleton ?? (
            <div className="space-y-4 animate-pulse">
              <div className="h-6 bg-gray-100 rounded w-48" />
              <SkeletonFormGrid fields={4} />
            </div>
          )
        ) : (
          <>
            {children}

            <div className="px-6 py-3 border-t border-gray-100 flex items-center justify-end gap-2">
              <Button variant="secondary" onClick={onCancel}>
                Cancel
              </Button>
              <Button
                variant="primary"
                onClick={onContinue}
                loading={submitting}
                disabled={!!continueDisabled || !!submitting}
              >
                {isDetailEdit
                  ? submitting
                    ? "Saving..."
                    : "Save"
                  : continueLabel}
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
