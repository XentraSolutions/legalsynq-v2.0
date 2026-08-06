"use client";

import { useContactDetailContext } from "./context";
import { EntityTimeline } from "@/components/lien/entity-timeline";

export function ContactActivitiesTab() {
  const { contact } = useContactDetailContext();
  return <EntityTimeline entityType="Contact" entityId={contact.id} />;
}
