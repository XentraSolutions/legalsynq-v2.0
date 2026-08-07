import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import {
  ColumnGroup,
  ReportColumnOption,
  ReportListResponse,
  ReportsResponse,
  ExportReportRequest,
  CreateReports,
  ReportTotals,
} from "@/lib/liens/lien-report.types";

interface UseLienReportProps {
  id: string | number | undefined;
  initialPage?: number;
  initialPageSize?: number;
}

type SummaryTotals = {
  summaryTotals: ReportTotals;
};

export function useLienReport<TTemplate = any>({
  id,
  initialPage = 1,
  initialPageSize = 10,
}: UseLienReportProps) {
  // Local state for pagination
  const [pagination, setPagination] = useState({
    page: initialPage,
    pageSize: initialPageSize,
  });

  // 1. Fetch the base report
  const {
    data: report,
    isLoading: isReportLoading,
    error: reportError,
  } = useQuery({
    queryKey: ["lienReport", id],
    queryFn: () => lienReportsService.getReportsById(id?.toString() ?? ""),
    enabled: Boolean(id),
  });

  // 2. Fetch the paginated template dependent on the report and pagination state
  const {
    data: template,
    isLoading: isTemplateLoading,
    isFetching: isTemplateFetching,
    error: templateError,
  } = useQuery<TTemplate>({
    queryKey: ["lienReportTemplate", id, pagination.page, pagination.pageSize],
    queryFn: async () => {
      const generatedTemplate = await lienReportsService.generateTemplate({
        ...(report as ReportsResponse),
        limit: pagination.pageSize,
        page: pagination.page,
      });
      const totalCount = generatedTemplate?.totalCount ?? 0;
      const pageSize = pagination.pageSize || 10;

      return {
        ...generatedTemplate,
        page: pagination.page,
        limit: pageSize,
        totalCount,
        totalPages: totalCount > 0 ? Math.ceil(totalCount / pageSize) : 1,
      } as TTemplate;
    },
    enabled: Boolean(report) && Boolean(id),
    placeholderData: (previousData) => previousData,
    refetchOnWindowFocus: false,
  });

  // Handlers to update pagination from your UI components
  const handlePageChange = (newPage: number) => {
    setPagination((prev) => ({ ...prev, page: newPage }));
  };

  const handlePageSizeChange = (newPageSize: number) => {
    setPagination((prev) => ({ ...prev, pageSize: newPageSize, page: 1 })); // Reset to page 1 on size change
  };

  return {
    report,
    template,
    pagination,
    setPage: handlePageChange,
    setPageSize: handlePageSizeChange,
    isLoading: isReportLoading || (Boolean(report) && isTemplateLoading),
    isLoadingData: isTemplateFetching,
    error: reportError || templateError,
  };
}

export function useFetchReportColumns(
  reportType: "CASES" | "LIENS",
  report: ReportListResponse &
    SummaryTotals &
    ExportReportRequest &
    CreateReports,
) {
  // Use stable primitives in the queryKey instead of the entire heavy report object
  const {
    data: columnsResponse,
    isLoading: isColumnsLoading,
    error: columnsError,
  } = useQuery({
    queryKey: ["lienReportColumns", reportType, report?.config?.columns],
    queryFn: () => lienReportsService.getColumns(reportType),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
    enabled: Boolean(reportType),
  });

  const { ...columnGroups } = (columnsResponse ?? {}) as Record<
    string,
    unknown
  >;

  const excludedKeys = new Set([
    "isSuccess",
    "message",
    "reportType",
    "data",
    "defaultColumn",
  ]);

  const groupedCols: ColumnGroup[] = Object.entries(columnGroups)
    .filter(([key]) => !excludedKeys.has(key))
    .filter(([_, value]) => Array.isArray(value))
    .map(([key, value]) => ({
      key,
      value: value as ReportColumnOption[],
    }));

  if (!report?.config?.columns) {
    return {
      defaultColumns: groupedCols,
      columnsResponse,
      isColumnsLoading,
      columnsError,
    };
  }

  let sortOrder = 1;

  const globallyOrderedItems = groupedCols
    .flatMap((section) =>
      (section.value || []).map((item) => ({
        ...item,
        sectionKey: section.key,
      })),
    )
    .filter((item) => report.config?.columns.includes(item.key))
    .sort((a, b) => {
      const rawResponse = report.config?.columns;
      const defaultColsArray = Array.isArray(rawResponse)
        ? (rawResponse as string[])
        : [];

      const indexA = defaultColsArray.indexOf(a.key);
      const indexB = defaultColsArray.indexOf(b.key);
      return (
        (indexA === -1 ? Infinity : indexA) -
        (indexB === -1 ? Infinity : indexB)
      );
    })
    .map((item) => ({
      ...item,
      sortOrder: sortOrder++,
    }));

  const selected = groupedCols
    .map((section) => {
      const sectionItems = globallyOrderedItems.filter(
        (item) => item.sectionKey === section.key,
      );

      return {
        key: section.key,
        value: sectionItems,
      };
    })
    .filter((section) => section.value.length > 0);

  const selectedValues = selected
    .flatMap((section) =>
      section.value.map((item: any) => ({
        ...item,
        sectionKey: section.key,
      })),
    )
    .sort((a, b) => a.sortOrder - b.sortOrder);

  return {
    defaultColumns: selectedValues,
    columnsResponse,
    isColumnsLoading,
    columnsError,
  };
}

export function useReportFilterOptions({
  reportType,
  filterField,
  keyword = "",
  enabled,
}: {
  reportType: "CASES" | "LIENS" | "COMBINE";
  filterField: string;
  keyword?: string;
  enabled?: boolean;
}) {
  const query = useQuery({
    queryKey: [
      "report-filter-options",
      reportType,
      filterField,
      keyword,
      enabled,
    ],

    queryFn: async () => {
      const filters = await lienReportsService.getFilterOptions({
        reportType: "COMBINE",
        filterField: filterField,
        keyword,
      });

      return (
        filters?.map((item: any) => ({
          key: item.id,
          value: item.id,
          label: item.name,
        })) ?? []
      );
    },

    enabled: enabled,
    staleTime: 0,

    placeholderData: undefined,
  });

  return {
    options: query.data ?? [],
    isLoadingFilter: query.isLoading || query.isFetching,
    refetch: query.refetch,
  };
}

import { useEffect } from "react";

export function useDebounce<T>(value: T, delay = 500) {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(timer);
    };
  }, [value, delay]);

  return debouncedValue;
}
