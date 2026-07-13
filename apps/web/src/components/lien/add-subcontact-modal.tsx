"use client";

import { AddContactModal } from "@/components/lien/add-contact-modal";
import type { ComponentProps } from "react";

type AddSubContactModalProps = Omit<
  ComponentProps<typeof AddContactModal>,
  "hideAddress"
>;

/** Sub-contacts (facility staff, law firm contacts) attach to a parent
 * entity's address, so the form omits address/city/state/zip fields. */
export function AddSubContactModal(props: AddSubContactModalProps) {
  return <AddContactModal {...props} hideAddress />;
}
