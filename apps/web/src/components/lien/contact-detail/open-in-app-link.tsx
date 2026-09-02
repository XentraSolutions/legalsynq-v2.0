import { forwardRef, type ComponentPropsWithoutRef } from "react";

import {
  DeepLinkError,
  buildDeepLink,
  type BuildDeepLinkInput,
} from "@/lib/deep-links";

type DeepLinkBuilder = (input: BuildDeepLinkInput) => string;

interface OpenInAppLinkProps extends ComponentPropsWithoutRef<"a"> {
  contactId: string | null | undefined;
  builder?: DeepLinkBuilder;
}

export const OpenInAppLink = forwardRef<
  HTMLAnchorElement,
  OpenInAppLinkProps
>(function OpenInAppLink(
  { contactId, builder = buildDeepLink, ...anchorProps },
  ref,
) {
  if (!contactId?.trim()) return null;

  try {
    const href = builder({
      routeKey: "contactDetails",
      pathParams: { contactId },
    });

    return (
      <a {...anchorProps} ref={ref} href={href}>
        <i className="ri-smartphone-line" aria-hidden="true" />
        Open in App
      </a>
    );
  } catch (error) {
    if (error instanceof DeepLinkError) return null;
    throw error;
  }
});
