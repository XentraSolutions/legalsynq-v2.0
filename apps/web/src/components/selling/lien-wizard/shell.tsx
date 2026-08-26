"use client";

import Link from "next/link";
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

export interface LienWizardShellProps {
  step: number;
  hydrating?: boolean;
  // Shown in place of the hydration spinner, sized to closely match the
  // step's real content so nothing jumps once data arrives. Falls back to a
  // generic form-field grid for steps that don't pass one.
  skeleton?: React.ReactNode;
  submitting?: boolean;
  continueDisabled?: boolean;
  continueLabel?: string;
  onBack: () => void;
  onContinue: () => void;
  children: React.ReactNode;
  // When set, this step is being opened as a standalone edit from the lien
  // detail page (via a "returnTo=detail" query param) rather than as part of
  // the create/edit wizard: hides the step progress bar, and the footer
  // reads Cancel/Save instead of Back/Continue. The top-left arrow returns
  // here directly instead of going to the lien list.
  detailEditReturnHref?: string;
}

// Shared chrome for every lien-wizard step page: back link, progress bar,
// hydration spinner, and the Back/Continue footer. Each step component owns
// its own form rendering and submit logic and just plugs into this shell.
export function LienWizardShell({
  step,
  hydrating,
  skeleton,
  submitting,
  continueDisabled,
  continueLabel = "Continue",
  onBack,
  onContinue,
  children,
  detailEditReturnHref,
}: LienWizardShellProps) {
  const isDetailEdit = !!detailEditReturnHref;
  return (
    <div className="max-w-[700px] m-auto">
      <div className="flex items-center mb-6 ">
        <nav>
          <Link
            href={detailEditReturnHref ?? "/selling/portfolio/lien"}
            className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors shrink-0"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
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
              <Button variant="secondary" onClick={onBack}>
                {isDetailEdit ? "Cancel" : "Back"}
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
