"use client";

import { useContactDetailContext } from "./context";
import { ContactCasesSection } from "@/components/lien/contact-cases-section";

export function ContactCasesTab() {
  const { contact, primaryButtonClassName } = useContactDetailContext();
  return (
    <ContactCasesSection
      contactId={contact.id}
      contactType={contact.contactType}
      primaryButtonClassName={primaryButtonClassName}
    />
  );
}
