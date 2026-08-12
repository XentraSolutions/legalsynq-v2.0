// app/providers.tsx

"use client";

import { QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { getQueryClient } from "@/lib/query-client";
import { ToastContainer } from "../toast-container";
import { ToastContainer as LienToastContainer } from "../lien/toast-container";

export default function SellingProviders({
  children,
}: {
  children: React.ReactNode;
}) {
  const [queryClient] = useState(getQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <ToastContainer />
      {/* Selling reuses several sync-liens components (e.g. AddContactModal)
          that report success/error via the lien-store toast, not toast-context. */}
      <LienToastContainer />
      {children}
    </QueryClientProvider>
  );
}
