"use client";

import { useContactDetailContext } from "../contact-detail-context";
import { ContactCasesSection } from "@/components/lien/contact-cases-section";

export default function ContactCasesPage() {
  const { contact } = useContactDetailContext();
  return (
    <ContactCasesSection contactId={contact.id} contactType={contact.contactType} />
  );
}
