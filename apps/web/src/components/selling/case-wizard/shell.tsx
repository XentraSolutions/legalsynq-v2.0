"use client";

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { Button } from "@/components/selling/button";
import { SkeletonFormGrid } from "@/components/lien/skeleton-loader";

function ProgressBar({
  currentStep,
  totalSteps,
}: {
  currentStep: number;
  totalSteps: number;
}) {
  return (
    <div className="w-full mx-auto p-4">
      <div className="flex w-full gap-2">
        {Array.from({ length: totalSteps }, (_, index) => {
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
  totalSteps: number;
  hydrating?: boolean;
  skeleton?: React.ReactNode;
  submitting?: boolean;
  continueDisabled?: boolean;
  continueLabel?: string;
  onBack: () => void;
  onContinue: () => void;
  children: React.ReactNode;
}

// Shared chrome for the case-wizard steps — same shape as
// LienWizardShell (@/components/selling/lien-wizard/shell), but takes
// totalSteps as a prop instead of a module constant since this wizard's step
// count (2) is independent of the lien wizard's (4).
export function CaseWizardShell({
  step,
  totalSteps,
  hydrating,
  skeleton,
  submitting,
  continueDisabled,
  continueLabel = "Continue",
  onBack,
  onContinue,
  children,
}: CaseWizardShellProps) {
  return (
    <div className="max-w-[700px] m-auto">
      <div className="flex items-center mb-6 ">
        <nav>
          <Link
            href="/selling/portfolio/cases"
            className="text-sm text-gray-500 hover:text-gray-800"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
        </nav>
        <ProgressBar currentStep={step} totalSteps={totalSteps} />
      </div>
      <p className={`mt-2 text-xs text-gray-600`}>
        Step {step}/ {totalSteps}
      </p>
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
              <Button variant="secondary" onClick={onBack}>
                Back
              </Button>
              <Button
                variant="primary"
                onClick={onContinue}
                loading={submitting}
                disabled={!!continueDisabled || !!submitting}
              >
                {continueLabel}
              </Button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
