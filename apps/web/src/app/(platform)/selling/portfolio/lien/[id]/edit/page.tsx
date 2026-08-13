"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";

// /selling/portfolio/lien/[id]/edit — redirects to step-1, so entry points
// like the pending list's "Edit" action always land at the start of the
// wizard.
export default function EditLienPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();

  useEffect(() => {
    const lienId = params?.id;
    if (!lienId) return;
    router.replace(`/selling/portfolio/lien/${lienId}/edit/step-1`);
  }, [params?.id, router]);

  return (
    <div className="py-16 text-center">
      <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
    </div>
  );
}
