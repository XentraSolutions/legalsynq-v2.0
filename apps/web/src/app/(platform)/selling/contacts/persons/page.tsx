"use client";

import { PageHeader } from "@/components/lien/page-header";
import { ContactsTabs } from "@/components/selling/contacts/contacts-tabs";
import { ContactPersonsDirectoryView } from "@/components/selling/contacts/contact-persons-directory-view";

export const dynamic = "force-dynamic";

export default function SellingContactPersonsPage() {
  return (
    <div className="space-y-5">
      <PageHeader
        card={false}
        title="Contacts"
        subtitle="Keep your company and contact person records organized and easily accessible."
      />

      <ContactsTabs active="persons" />

      <ContactPersonsDirectoryView />
    </div>
  );
}
