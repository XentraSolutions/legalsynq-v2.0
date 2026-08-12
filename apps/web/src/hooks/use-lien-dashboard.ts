import { useQuery } from "@tanstack/react-query";
import { casesService } from "@/lib/cases";
import type { DateRangeValue } from "@/components/ui/date-range-picker";

export type DashboardReportKey = "liens" | "cases" | "lawFirm" | "facility";

const DASHBOARD_SUMMARY_PAGE_SIZE = 1;
export const DASHBOARD_DETAIL_PAGE_SIZE = 10;

export function useDashboardStats() {
  return useQuery({
    queryKey: ["dashboard-stats"],
    queryFn: () => casesService.getDashboardStats(),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

export function useDashboardReports(range: DateRangeValue) {
  return useQuery({
    queryKey: ["dashboard-reports", range.from, range.to],
    queryFn: async () => {
      const request = {
        page: 1,
        limit: DASHBOARD_SUMMARY_PAGE_SIZE,
        startDate: range.from,
        endDate: range.to,
      };
      const [lawFirms, facilities, liens, cases, deployed, received] =
        await Promise.all([
          casesService.getLawFirmCaseAllocation(request),
          casesService.getMedicalFacilityCaseAllocation(request),
          casesService.getTotalLienReportRows(request),
          casesService.getTotalCaseReportRows(request),
          casesService.getCashDeployed(request),
          casesService.getCashReceived(request),
        ]);
      return { lawFirms, facilities, liens, cases, deployed, received };
    },
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}

export function useDashboardReportDetails(
  report: DashboardReportKey | null,
  range: DateRangeValue,
  page: number,
) {
  return useQuery({
    queryKey: ["dashboard-report-details", report, range.from, range.to, page],
    queryFn: async () => {
      const request = {
        page,
        limit: DASHBOARD_DETAIL_PAGE_SIZE,
        startDate: range.from,
        endDate: range.to,
      };

      switch (report) {
        case "liens": {
          const result = await casesService.getTotalLienReportRows(request);
          return { rows: result.items, totalCount: result.totalCount };
        }
        case "cases": {
          const result = await casesService.getTotalCaseReportRows(request);
          return { rows: result.items, totalCount: result.totalCount };
        }
        case "lawFirm": {
          const result = await casesService.getLawFirmCaseAllocation(request);
          return { rows: result.rows, totalCount: result.totalCount };
        }
        case "facility": {
          const result = await casesService.getMedicalFacilityCaseAllocation(request);
          return { rows: result.rows, totalCount: result.totalCount };
        }
        default:
          return { rows: [], totalCount: 0 };
      }
    },
    enabled: report !== null,
    staleTime: 30_000,
    refetchOnWindowFocus: false,
  });
}
