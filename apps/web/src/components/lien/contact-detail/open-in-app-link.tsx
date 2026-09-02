import { forwardRef, type ComponentPropsWithoutRef } from "react";

import {
  OpenInAppLink as SharedOpenInAppLink,
  type DeepLinkBuilder,
} from "@/components/open-in-app-link";

interface OpenInAppLinkProps extends ComponentPropsWithoutRef<"a"> {
  contactId: string | null | undefined;
  builder?: DeepLinkBuilder;
}

export const OpenInAppLink = forwardRef<
  HTMLAnchorElement,
  OpenInAppLinkProps
>(function OpenInAppLink({ contactId, builder, ...anchorProps }, ref) {
  if (!contactId?.trim()) return null;

  return (
    <SharedOpenInAppLink
      {...anchorProps}
      ref={ref}
      intent={{
        routeKey: "contactDetails",
        pathParams: { contactId },
      }}
      builder={builder}
    />
  );
});
