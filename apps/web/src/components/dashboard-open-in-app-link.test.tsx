import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";

import type { BuildDeepLinkInput } from "@/lib/deep-links";
import { DashboardOpenInAppLink } from "./dashboard-open-in-app-link";

const originalBaseUrl = process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

afterEach(() => {
  if (originalBaseUrl === undefined) {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
  } else {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = originalBaseUrl;
  }
});

describe("Dashboard Open in App", () => {
  test("renders the canonical same-context Dashboard link", () => {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test/";

    render(<DashboardOpenInAppLink />);

    const link = screen.getByRole("link", { name: "Open in App" });
    expect(link).toHaveAttribute("href", "https://links.example.test/dashboard");
    expect(link).not.toHaveAttribute("target");
  });

  test("passes only the parameterless Dashboard route intent", () => {
    const builder = vi.fn<(input: BuildDeepLinkInput) => string>(() =>
      "https://links.example.test/dashboard",
    );

    render(<DashboardOpenInAppLink builder={builder} />);

    expect(builder).toHaveBeenCalledOnce();
    expect(builder).toHaveBeenCalledWith({ routeKey: "dashboard" });
  });

  test("hides the action when deep-link configuration is unavailable", () => {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

    render(<DashboardOpenInAppLink />);

    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });

  test("does not mask unexpected builder failures", () => {
    const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    const builder = vi.fn(() => {
      throw new Error("unexpected failure");
    });

    try {
      expect(() => render(<DashboardOpenInAppLink builder={builder} />)).toThrow(
        "unexpected failure",
      );
    } finally {
      consoleError.mockRestore();
    }
  });
});
