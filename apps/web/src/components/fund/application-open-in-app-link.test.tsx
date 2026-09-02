import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";

import type { BuildDeepLinkInput } from "@/lib/deep-links";
import { ApplicationOpenInAppLink } from "./application-open-in-app-link";

const originalBaseUrl = process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

afterEach(() => {
  if (originalBaseUrl === undefined) {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
  } else {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = originalBaseUrl;
  }
});

describe("Application Details Open in App", () => {
  test("renders an accessible same-context link from the real builder", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test/";

    render(
      <ApplicationOpenInAppLink applicationId="11111111-1111-1111-1111-111111111111" />,
    );

    const link = screen.getByRole("link", { name: "Open in App" });
    expect(link).toHaveAttribute(
      "href",
      "https://links.example.test/applications/11111111-1111-1111-1111-111111111111",
    );
    expect(link).not.toHaveAttribute("target");
  });

  test("passes canonical Application route intent and ID to the builder", () => {
    const builder = vi.fn<(input: BuildDeepLinkInput) => string>(() =>
      "https://links.example.test/applications/application-123",
    );

    render(
      <ApplicationOpenInAppLink
        applicationId="application-123"
        builder={builder}
      />,
    );

    expect(builder).toHaveBeenCalledOnce();
    expect(builder).toHaveBeenCalledWith({
      routeKey: "applicationDetails",
      pathParams: { applicationId: "application-123" },
    });
  });

  test("preserves builder encoding for an Application ID", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test";

    render(<ApplicationOpenInAppLink applicationId="application/123 #1" />);

    expect(screen.getByRole("link", { name: "Open in App" })).toHaveAttribute(
      "href",
      "https://links.example.test/applications/application%2F123%20%231",
    );
  });

  test("hides the action when deep-link configuration is unavailable", () => {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

    render(<ApplicationOpenInAppLink applicationId="application-123" />);

    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });

  test.each([undefined, null, "", "   "])(
    "hides the action without invoking the builder for applicationId %s",
    (applicationId) => {
      const builder = vi.fn<(input: BuildDeepLinkInput) => string>();

      render(
        <ApplicationOpenInAppLink
          applicationId={applicationId}
          builder={builder}
        />,
      );

      expect(
        screen.queryByRole("link", { name: "Open in App" }),
      ).not.toBeInTheDocument();
      expect(builder).not.toHaveBeenCalled();
    },
  );
});
