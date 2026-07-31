// app/providers.tsx

"use client";

import { QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { getQueryClient } from "@/lib/query-client";
import { ToastContainer } from "../toast-container";

export default function SellingProviders({
  children,
}: {
  children: React.ReactNode;
}) {
  const [queryClient] = useState(getQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <ToastContainer />
      {children}
    </QueryClientProvider>
  );
}
