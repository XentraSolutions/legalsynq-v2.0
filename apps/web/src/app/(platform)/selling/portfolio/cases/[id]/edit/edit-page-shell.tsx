"use client";

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { Button } from "@/components/selling/button";

// Shared shell for the case-info and plaintiff edit pages — plain form
// layout, not CaseWizardShell (that one carries step-progress/Back-Continue
// machinery that belongs to the create flow, not a single-field-set edit).
export function EditPageShell({
  backHref,
  title,
  subtitle,
  onCancel,
  onSave,
  saving,
  children,
}: {
  backHref: string;
  title: string;
  subtitle: string;
  onCancel: () => void;
  onSave: () => void;
  saving?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="max-w-[700px] mx-auto space-y-5">
      <Link
        href={backHref}
        aria-label="Back"
        className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors"
      >
        <ArrowLeft className="h-5 w-5" />
      </Link>

      <div>
        <h1 className="text-2xl font-bold text-gray-900">{title}</h1>
        <p className="text-sm text-gray-500 mt-1">{subtitle}</p>
      </div>

      {children}

      <div className="flex items-center justify-end gap-3">
        <Button variant="secondary" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button variant="primary" onClick={onSave} loading={saving}>
          Save
        </Button>
      </div>
    </div>
  );
}
