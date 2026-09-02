import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";

import { ApiError } from "@/lib/api-client";
import type { FundingApplicationDetail } from "@/types/fund";
import ApplicationDetailPage from "./page";

const mocks = vi.hoisted(() => {
  const push = vi.fn();
  return {
    getById: vi.fn(),
    push,
    router: { push },
    useSession: vi.fn(),
  };
});

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: "route-application-id" }),
  useRouter: () => mocks.router,
}));

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock("@/hooks/use-session", () => ({
  useSession: mocks.useSession,
}));

vi.mock("@/lib/fund-api", () => ({
  fundApi: { applications: { getById: mocks.getById } },
}));

vi.mock("@/components/fund/submit-application-panel", () => ({
  SubmitApplicationPanel: () => <div>Submit application action</div>,
}));

vi.mock("@/components/fund/review-decision-panel", () => ({
  ReviewDecisionPanel: () => <div>Review application action</div>,
}));

const application: FundingApplicationDetail = {
  id: "11111111-1111-1111-1111-111111111111",
  tenantId: "tenant-1",
  applicationNumber: "APP-0001",
  applicantFirstName: "Ada",
  applicantLastName: "Lovelace",
  email: "ada@example.test",
  phone: "555-0100",
  requestedAmount: 10000,
  caseType: "Personal Injury",
  status: "Draft",
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-02T00:00:00Z",
};

beforeEach(() => {
  vi.clearAllMocks();
  process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = "https://links.example.test";
  mocks.useSession.mockReturnValue({
    session: { productRoles: ["SYNQ_FUND:SYNQFUND_REFERRER"] },
    isLoading: false,
  });
});

describe("Application Details Open in App states", () => {
  test("does not render a deep link while the Application identity is loading", () => {
    mocks.useSession.mockReturnValue({ session: null, isLoading: true });

    const { container } = render(<ApplicationDetailPage />);

    expect(container.querySelector(".animate-pulse")).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
    expect(mocks.getById).not.toHaveBeenCalled();
  });

  test("preserves not-found behavior without exposing a resource link", async () => {
    mocks.getById.mockRejectedValue(
      new ApiError(404, "Application request failed", "correlation-1"),
    );

    render(<ApplicationDetailPage />);

    expect(await screen.findByText("Application not found.")).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Open in App" }),
    ).not.toBeInTheDocument();
  });

  test("uses the loaded canonical ID and preserves the existing workflow action", async () => {
    mocks.getById.mockResolvedValue({ data: application });

    render(<ApplicationDetailPage />);

    expect(
      await screen.findByRole("link", { name: "Open in App" }),
    ).toHaveAttribute(
      "href",
      "https://links.example.test/applications/11111111-1111-1111-1111-111111111111",
    );
    expect(screen.getByText("Submit application action")).toBeInTheDocument();
    expect(mocks.getById).toHaveBeenCalledWith("route-application-id");
  });
});
