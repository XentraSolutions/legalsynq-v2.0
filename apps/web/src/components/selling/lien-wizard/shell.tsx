"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { TOTAL_STEPS } from "./shared";

// Selling's brand accent, matching the convention used on other selling pages.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white disabled:bg-[#EE7132]/70";

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

export interface LienWizardShellProps {
  step: number;
  hydrating?: boolean;
  submitting?: boolean;
  continueDisabled?: boolean;
  continueLabel?: string;
  onBack: () => void;
  onContinue: () => void;
  children: React.ReactNode;
}

// Shared chrome for every lien-wizard step page: back link, progress bar,
// hydration spinner, and the Back/Continue footer. Each step component owns
// its own form rendering and submit logic and just plugs into this shell.
export function LienWizardShell({
  step,
  hydrating,
  submitting,
  continueDisabled,
  continueLabel = "Continue",
  onBack,
  onContinue,
  children,
}: LienWizardShellProps) {
  return (
    <div className="max-w-[700px] m-auto">
      <div className="flex items-center mb-6 ">
        <nav>
          <Link
            href="/selling/portfolio"
            className="text-sm text-gray-500 hover:text-gray-800"
          >
            <i className="ri-arrow-left-line text-xl" />
          </Link>
        </nav>
        <ProgressBar currentStep={step} />
      </div>
      <p className={`mt-2 text-xs text-gray-600`}>
        Step {step}/ {TOTAL_STEPS}
      </p>
      <div className="mt-5 position-relative ">
        {hydrating ? (
          <div className="py-16 text-center">
            <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          </div>
        ) : (
          <>
            {children}

            <div className="px-6 py-3 border-t border-gray-100 flex items-center justify-end gap-2">
              <Button variant="secondary" onClick={onBack}>
                Back
              </Button>
              <Button
                className={PRIMARY_BUTTON_CLASSNAME}
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
