import type { ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { setupServer } from "msw/node";
import { afterAll, afterEach, beforeAll, describe, expect, test } from "vitest";
import {
  DASHBOARD_DETAIL_PAGE_SIZE,
  useDashboardReportDetails,
  useDashboardReports,
} from "./use-lien-dashboard";

const reportPaths = [
  "lawfirm-case-report-export/v3",
  "medical-provider-report-export/v3",
  "total-lien-report-export/v3",
  "total-case-report-export/v3",
];

const requestBodies = new Map<string, unknown>();

const server = setupServer(
  ...reportPaths.map((path) =>
    http.post(`/api/lien/api/liens/cases/dashboard/${path}`, async ({ request }) => {
      requestBodies.set(path, await request.json());
      return HttpResponse.json({
        items: [],
        page: 1,
        pageSize: 1,
        totalCount: 42,
        statusCounts: { Open: 42 },
        statusAmounts: { Open: { purchase: 100, billing: 200 } },
        allocationCounts: { Example: 42 },
      });
    }),
  ),
  http.post("/api/lien/api/liens/cases/dashboard/deployed", () =>
    HttpResponse.json({
      data: { periodStart: "", periodEnd: "", totalAmount: "100" },
    }),
  ),
  http.post("/api/lien/api/liens/cases/dashboard/cash-received", () =>
    HttpResponse.json({
      data: { periodStart: "", periodEnd: "", totalAmount: "50" },
    }),
  ),
);

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => {
  requestBodies.clear();
  server.resetHandlers();
});
afterAll(() => server.close());

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };
}

describe("lien dashboard report queries", () => {
  test("requests only one representative row for the dashboard summaries", async () => {
    const { result } = renderHook(
      () => useDashboardReports({ from: "2026-08-01", to: "2026-08-06" }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    for (const path of reportPaths) {
      expect(requestBodies.get(path)).toMatchObject({
        page: 1,
        limit: 1,
        startDate: "08/01/2026",
        endDate: "08/06/2026",
      });
    }
    expect(result.current.data?.liens.totalCount).toBe(42);
    expect(result.current.data?.liens.statusCounts).toEqual({ Open: 42 });
  });

  test("loads report rows on demand using server-side pagination", async () => {
    const { result } = renderHook(
      () =>
        useDashboardReportDetails(
          "liens",
          { from: "2026-08-01", to: "2026-08-06" },
          3,
        ),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(requestBodies.get("total-lien-report-export/v3")).toMatchObject({
      page: 3,
      limit: DASHBOARD_DETAIL_PAGE_SIZE,
    });
    expect(result.current.data).toEqual({ rows: [], totalCount: 42 });
  });
});
