import { useQuery } from "@tanstack/react-query";
import { casesService } from "@/lib/cases";
import type { DateRangeValue } from "@/components/ui/date-range-picker";

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
        limit: 500,
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
