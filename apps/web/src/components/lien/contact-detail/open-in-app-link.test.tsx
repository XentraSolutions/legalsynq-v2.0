import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";

import type { BuildDeepLinkInput } from "@/lib/deep-links";
import { OpenInAppLink } from "./open-in-app-link";

const originalBaseUrl = process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

afterEach(() => {
  if (originalBaseUrl === undefined) {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
  } else {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = originalBaseUrl;
  }
});

describe("Contact Details Open in App", () => {
  test("renders an accessible same-context link from the real builder", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test/";

    render(<OpenInAppLink contactId="contact-123" />);

    const link = screen.getByRole("link", { name: "Open in App" });
    expect(link).toHaveAttribute(
      "href",
      "https://links.example.test/contacts/contact-123",
    );
    expect(link).not.toHaveAttribute("target");
  });

  test("passes canonical Contact route intent and ID to the builder", () => {
    const builder = vi.fn<(input: BuildDeepLinkInput) => string>(() =>
      "https://links.example.test/contacts/contact-123",
    );

    render(<OpenInAppLink contactId="contact-123" builder={builder} />);

    expect(builder).toHaveBeenCalledOnce();
    expect(builder).toHaveBeenCalledWith({
      routeKey: "contactDetails",
      pathParams: { contactId: "contact-123" },
    });
  });

  test("preserves builder encoding for a Contact ID", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test";

    render(<OpenInAppLink contactId="contact/123 #1" />);

    expect(screen.getByRole("link", { name: "Open in App" })).toHaveAttribute(
      "href",
      "https://links.example.test/contacts/contact%2F123%20%231",
    );
  });

  test("hides the action when deep-link configuration is unavailable", () => {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

    render(<OpenInAppLink contactId="contact-123" />);

    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });

  test("does not hide unexpected programming failures", () => {
    const failure = new Error("unexpected builder defect");
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    const builder = vi.fn<(input: BuildDeepLinkInput) => string>(() => {
      throw failure;
    });

    try {
      expect(() =>
        render(<OpenInAppLink contactId="contact-123" builder={builder} />),
      ).toThrow(failure);
    } finally {
      consoleError.mockRestore();
    }
  });

  test.each([undefined, null, "", "   "])(
    "hides the action without invoking the builder for contactId %s",
    (contactId) => {
      const builder = vi.fn<(input: BuildDeepLinkInput) => string>();

      render(<OpenInAppLink contactId={contactId} builder={builder} />);

      expect(
        screen.queryByRole("link", { name: "Open in App" }),
      ).not.toBeInTheDocument();
      expect(builder).not.toHaveBeenCalled();
    },
  );
});
