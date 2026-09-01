"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";

// /selling/portfolio/cases/[id]/edit — redirects to step-1, so entry points
// like the cases list's "Edit" action always land at the start of the full
// multi-step wizard. Mirrors the lien wizard's equivalent redirect
// (@/app/(platform)/selling/portfolio/lien/[id]/edit/page.tsx).
export default function EditCasePage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();

  useEffect(() => {
    const caseId = params?.id;
    if (!caseId) return;
    router.replace(`/selling/portfolio/cases/${caseId}/edit/step-1`);
  }, [params?.id, router]);

  return (
    <div className="py-16 text-center">
      <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
    </div>
  );
}
