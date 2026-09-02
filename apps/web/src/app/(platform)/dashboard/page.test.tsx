import type { ReactNode } from "react";
import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";

import DashboardPage from "./page";

const mocks = vi.hoisted(() => ({
  getServerPortalConfig: vi.fn(),
  redirect: vi.fn(),
  requireOrg: vi.fn(),
  resolveEnabledNavKeys: vi.fn(),
}));

vi.mock("next/headers", () => ({
  headers: async () => new Headers({ host: "app.example.test" }),
}));

vi.mock("next/navigation", () => ({
  redirect: mocks.redirect,
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
  resolveEnabledNavKeys: mocks.resolveEnabledNavKeys,
}));

beforeEach(() => {
  vi.clearAllMocks();
  mocks.getServerPortalConfig.mockReturnValue(null);
  mocks.resolveEnabledNavKeys.mockReturnValue(new Set(["fund"]));
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

describe("Dashboard", () => {
  test("preserves the welcome content, product card, and administration destinations", async () => {
    const page = await DashboardPage();

    render(page);

    const heading = screen.getByRole("heading", {
      name: "Welcome back, Example Legal",
    });
    expect(heading.parentElement).not.toHaveAttribute("class");
    expect(screen.queryByRole("link", { name: "Open in App" })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /SynqFund/ })).toHaveAttribute(
      "href",
      "/fund/applications",
    );
    expect(screen.getByRole("link", { name: /Users/ })).toHaveAttribute(
      "href",
      "/admin/users",
    );
    expect(screen.getByRole("link", { name: /Organizations/ })).toHaveAttribute(
      "href",
      "/admin/organizations",
    );
    expect(mocks.requireOrg).toHaveBeenCalledOnce();
  });

  test("preserves product filtering and hides administration for non-admin users", async () => {
    mocks.resolveEnabledNavKeys.mockReturnValue(new Set());
    mocks.requireOrg.mockResolvedValue({
      email: "user@example.test",
      enabledProducts: [],
      isPlatformAdmin: false,
      isTenantAdmin: false,
      orgId: "org-1",
      orgName: "Example Legal",
      orgType: "LAW_FIRM",
      userProducts: [],
    });

    render(await DashboardPage());

    expect(screen.getByText("No products assigned.")).toBeInTheDocument();
    expect(screen.queryByText("Administration")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /SynqFund/ })).not.toBeInTheDocument();
  });

  test("preserves product-specific portal redirects after the organization guard", async () => {
    mocks.getServerPortalConfig.mockReturnValue({
      landingPath: "/fund/applications",
    });

    await DashboardPage();

    expect(mocks.requireOrg).toHaveBeenCalledOnce();
    expect(mocks.getServerPortalConfig).toHaveBeenCalledWith("app.example.test");
    expect(mocks.redirect).toHaveBeenCalledWith("/fund/applications");
    expect(mocks.requireOrg.mock.invocationCallOrder[0]).toBeLessThan(
      mocks.getServerPortalConfig.mock.invocationCallOrder[0],
    );
    expect(mocks.getServerPortalConfig.mock.invocationCallOrder[0]).toBeLessThan(
      mocks.redirect.mock.invocationCallOrder[0],
    );
  });
});
