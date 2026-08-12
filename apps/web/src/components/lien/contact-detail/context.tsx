"use client";

import { createContext, useContext } from "react";
import type { ContactDetail } from "@/lib/contacts";

export type ContactDetailContextValue = {
  id: string;
  contact: ContactDetail;
  canEdit: boolean;
  refetch: () => Promise<void>;
  /** Route prefix for this product's contacts feature (e.g. "/lien/contacts" or "/selling/contacts"). */
  basePath: string;
  /** Overrides the default bg-primary styling on primary action buttons within the detail view (e.g. selling's orange brand). */
  primaryButtonClassName?: string;
};

const ContactDetailContext = createContext<ContactDetailContextValue | null>(
  null,
);

export function ContactDetailContextProvider({
  value,
  children,
}: {
  value: ContactDetailContextValue;
  children: React.ReactNode;
}) {
  return (
    <ContactDetailContext.Provider value={value}>
      {children}
    </ContactDetailContext.Provider>
  );
}

export function useContactDetailContext(): ContactDetailContextValue {
  const ctx = useContext(ContactDetailContext);
  if (!ctx) {
    throw new Error(
      "useContactDetailContext must be used within a ContactDetailContextProvider",
    );
  }
  return ctx;
}
