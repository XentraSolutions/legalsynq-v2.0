"use client";

import { useContactDetailContext } from "./context";
import { MedicalFacilityStaffSection } from "@/components/lien/medical-facility-staff-section";
import { LawFirmContactSection } from "@/components/lien/law-firm-contact-section";

export function ContactStaffTab() {
  const { contact, primaryButtonClassName } = useContactDetailContext();

  if (contact.contactType === "MedicalFacility" && !contact.contactSubtype) {
    return (
      <MedicalFacilityStaffSection
        facilityId={contact.id}
        primaryButtonClassName={primaryButtonClassName}
      />
    );
  }

  if (contact.contactType === "LawFirm") {
    return (
      <LawFirmContactSection
        lawFirmId={contact.id}
        primaryButtonClassName={primaryButtonClassName}
      />
    );
  }

  return null;
}
