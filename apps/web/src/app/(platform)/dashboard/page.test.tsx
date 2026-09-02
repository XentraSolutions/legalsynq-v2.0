import type { ReactNode } from "react";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";

import DashboardPage from "./page";

const mocks = vi.hoisted(() => ({
  getServerPortalConfig: vi.fn(),
  requireOrg: vi.fn(),
}));

vi.mock("next/headers", () => ({
  headers: async () => new Headers({ host: "app.example.test" }),
}));

vi.mock("next/navigation", () => ({
  redirect: vi.fn(),
}));

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock("@/lib/auth-guards", () => ({
  requireOrg: mocks.requireOrg,
}));

vi.mock("@/lib/portal", () => ({
  getServerPortalConfig: mocks.getServerPortalConfig,
}));

vi.mock("@/lib/nav", () => ({
  PRODUCT_META: {
    fund: {
      label: "SynqFund",
      icon: "ri-money-dollar-circle-line",
      color: "#15803d",
      iconSrc: "",
    },
  },
  PRODUCT_NAV: {
    fund: [{ items: [{ href: "/fund/applications", label: "Applications" }] }],
  },
  orgTypeLabel: () => "Law Firm",
  resolveEnabledNavKeys: () => new Set(["fund"]),
}));

beforeEach(() => {
  vi.clearAllMocks();
  process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test";
  mocks.getServerPortalConfig.mockReturnValue(null);
  mocks.requireOrg.mockResolvedValue({
    email: "user@example.test",
    enabledProducts: ["SynqFund"],
    isPlatformAdmin: false,
    isTenantAdmin: true,
    orgId: "org-1",
    orgName: "Example Legal",
    orgType: "LAW_FIRM",
    userProducts: ["SynqFund"],
  });
});

describe("Dashboard Open in App integration", () => {
  test("adds the action to the responsive welcome header without replacing Dashboard actions", async () => {
    const page = await DashboardPage();

    render(page);

    const heading = screen.getByRole("heading", {
      name: "Welcome back, Example Legal",
    });
    expect(heading.parentElement?.parentElement).toHaveClass(
      "flex-col",
      "sm:flex-row",
    );
    expect(screen.getByRole("link", { name: "Open in App" })).toHaveAttribute(
      "href",
      "https://links.example.test/dashboard",
    );
    expect(screen.getByRole("link", { name: /SynqFund/ })).toHaveAttribute(
      "href",
      "/fund/applications",
    );
    expect(screen.getByRole("link", { name: /Users/ })).toHaveAttribute(
      "href",
      "/admin/users",
    );
  });

  test("keeps Dashboard content available when deep-link configuration is missing", async () => {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;

    render(await DashboardPage());

    expect(
      screen.getByRole("heading", { name: "Welcome back, Example Legal" }),
    ).toBeInTheDocument();
    expect(screen.getByText("Your Products")).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });
});
