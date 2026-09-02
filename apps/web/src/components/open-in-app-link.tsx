import { forwardRef, type ComponentPropsWithoutRef } from "react";

import {
  DeepLinkError,
  buildDeepLink,
  type BuildDeepLinkInput,
} from "@/lib/deep-links";

export type DeepLinkBuilder = (input: BuildDeepLinkInput) => string;

interface OpenInAppLinkProps extends ComponentPropsWithoutRef<"a"> {
  intent: BuildDeepLinkInput;
  builder?: DeepLinkBuilder;
}

export const OpenInAppLink = forwardRef<HTMLAnchorElement, OpenInAppLinkProps>(
  function OpenInAppLink(
    { intent, builder = buildDeepLink, ...anchorProps },
    ref,
  ) {
    try {
      const href = builder(intent);

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
  },
);
