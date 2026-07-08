import type { PaginationMeta, PaginatedResultDto } from "./batch.types";

export function formatDateField(val: string | null | undefined): string {
  if (!val) return "";
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
      timeZone: "UTC",
    });
  } catch {
    return val;
  }
}
export const dateConverter = (dateData: string) => {
  if (!dateData) return "";

  const date = new Date(dateData);

  // Format the date using the US locale to automatically get MM/DD/YYYY
  const formatter = new Intl.DateTimeFormat("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
  });

  const formattedDate = formatter.format(date);
  return formattedDate;
};

export const dateConvertertoIso = (dateData: string) => {
  if (!dateData) return "";
  const d = new Date(dateData);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};

export function mapPagination<T>(
  result: PaginatedResultDto<T>,
): PaginationMeta {
  return {
    page: result.page,
    pageSize: result.pageSize,
    totalCount: result.totalCount,
    totalPages: Math.ceil(result.totalCount / Math.max(result.pageSize, 1)),
  };
}
