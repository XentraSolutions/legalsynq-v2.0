"use client";

import { useContactDetailContext } from "../contact-detail-context";
import { MedicalFacilityStaffSection } from "@/components/lien/medical-facility-staff-section";
import { LawFirmContactSection } from "@/components/lien/law-firm-contact-section";

export default function ContactStaffPage() {
  const { contact } = useContactDetailContext();

  if (contact.contactType === "MedicalFacility" && !contact.contactSubtype) {
    return <MedicalFacilityStaffSection facilityId={contact.id} />;
  }

  if (contact.contactType === "LawFirm") {
    return <LawFirmContactSection lawFirmId={contact.id} />;
  }

  return null;
}
