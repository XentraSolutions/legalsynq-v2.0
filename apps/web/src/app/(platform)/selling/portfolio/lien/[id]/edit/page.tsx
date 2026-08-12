"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ApiError } from "@/lib/api-client";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";

// /selling/portfolio/lien/[id]/edit — resolves which step to resume on (the
// same lien fetched by the detail view) and redirects to the real step
// route, so entry points like the pending list's "Edit" action don't need
// to know the lien's progress themselves.
export default function EditLienPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { show: showToast } = useToast();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const lienId = params?.id;
    if (!lienId) return;
    let cancelled = false;
    (async () => {
      try {
        const lien = await liensService.getLienById(lienId);
        if (cancelled) return;
        const resumeStep =
          lien.medicalPricing.rows.length > 0
            ? 4
            : lien.caseInformation?.lawFirmId || lien.fundingCompany
              ? 3
              : 2;
        router.replace(`/selling/portfolio/lien/${lienId}/edit/step-${resumeStep}`);
      } catch (err) {
        if (cancelled) return;
        const message =
          err instanceof ApiError
            ? err.message
            : err instanceof Error
              ? err.message
              : "Failed to load lien";
        setError(message);
        showToast(message, "error");
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params?.id]);

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700 max-w-[700px] mx-auto mt-6">
        {error}
      </div>
    );
  }

  return (
    <div className="py-16 text-center">
      <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
    </div>
  );
}
