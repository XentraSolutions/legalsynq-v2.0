import { createRef } from "react";
import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";

import { DeepLinkError, type BuildDeepLinkInput } from "@/lib/deep-links";
import { OpenInAppLink } from "./open-in-app-link";

const originalBaseUrl = process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

afterEach(() => {
  if (originalBaseUrl === undefined) {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
  } else {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = originalBaseUrl;
  }
});

describe("shared Open in App presentation", () => {
  test("renders the canonical HTTPS URL returned by DL-WEB-001", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test";

    render(<OpenInAppLink intent={{ routeKey: "dashboard" }} />);

    const link = screen.getByRole("link", { name: "Open in App" });
    expect(link).toHaveAttribute("href", "https://links.example.test/dashboard");
    expect(link).not.toHaveAttribute("target");
    expect(link.querySelector("i")).toHaveAttribute("aria-hidden", "true");
  });

  test("delegates feature-owned intent unchanged and forwards anchor behavior", () => {
    const intent: BuildDeepLinkInput = {
      routeKey: "contactDetails",
      pathParams: { contactId: "contact-123" },
    };
    const builder = vi.fn(() => "https://links.example.test/contacts/contact-123");
    const ref = createRef<HTMLAnchorElement>();

    render(
      <OpenInAppLink
        ref={ref}
        intent={intent}
        builder={builder}
        className="feature-owned-class"
        data-placement="contact-menu"
      />,
    );

    expect(builder).toHaveBeenCalledOnce();
    expect(builder).toHaveBeenCalledWith(intent);
    expect(ref.current).toBe(screen.getByRole("link", { name: "Open in App" }));
    expect(ref.current).toHaveClass("feature-owned-class");
    expect(ref.current).toHaveAttribute("data-placement", "contact-menu");
  });

  test("renders nothing when real builder configuration is unavailable", () => {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

    render(<OpenInAppLink intent={{ routeKey: "dashboard" }} />);

    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });

  test("contains expected builder failures", () => {
    const builder = vi.fn(() => {
      throw new DeepLinkError("UNKNOWN_ROUTE", "Unavailable route");
    });

    render(
      <OpenInAppLink
        intent={{ routeKey: "feature-owned-route" }}
        builder={builder}
      />,
    );

    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });

  test("does not hide unexpected failures", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    const failure = new Error("unexpected builder failure");
    const builder = vi.fn(() => {
      throw failure;
    });

    try {
      expect(() =>
        render(
          <OpenInAppLink
            intent={{ routeKey: "feature-owned-route" }}
            builder={builder}
          />,
        ),
      ).toThrow(failure);
    } finally {
      consoleError.mockRestore();
    }
  });
});
