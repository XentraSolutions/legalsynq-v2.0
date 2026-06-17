import { ReportConfigResponse } from "./lien-report.types";

function formatDateField(val: string | null | undefined): string {
  if (!val) return "";
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleDateString("en-US", {
      month: "short",
      day: "numeric",
      year: "numeric",
    });
  } catch {
    return val;
  }
}
export interface ReportListItem {
  id: string;
  name: string;
  description?: string | null | undefined;
  createdAt: string;
  updatedAt?: string;
  config: Record<string, unknown>;
}

export function mapReportToListItem(dto: ReportConfigResponse): ReportListItem {
  return {
    id: dto.id,
    name: dto.name,
    description: dto.description,
    createdAt: formatDateField(dto.createdAtUtc),
    updatedAt: formatDateField(dto.updatedAtUtc),
    config: dto.config,
  };
}
