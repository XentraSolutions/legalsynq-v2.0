"use client";

import { useContactDetailContext } from "../contact-detail-context";
import { EntityTimeline } from "@/components/lien/entity-timeline";

export default function ContactActivitiesPage() {
  const { contact } = useContactDetailContext();
  return <EntityTimeline entityType="Contact" entityId={contact.id} />;
}
