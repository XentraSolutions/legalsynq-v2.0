import type { Ionicons } from '@expo/vector-icons';

export type StatusTone = 'success' | 'warning' | 'danger' | 'info';
export type BreakdownItem = {
  id: string;
  key: string;
  status: string;
  statusColor?: string;
  statusTone?: StatusTone;
  showStatus?: boolean;
  fields: Array<{ icon: keyof typeof Ionicons.glyphMap; label: string; value: string }>;
};
export type ReportModel = {
  title: string;
  subtitle: string;
  breakdownTitle: string;
  breakdownItems: BreakdownItem[];
};
export type ReportPaginationMeta = {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};
export type BreakdownSortField =
  | 'caseId'
  | 'dateOfLoss'
  | 'entity'
  | 'name'
  | 'plaintiff'
  | 'status';
export type BreakdownSortDirection = 'asc' | 'desc';
export type BreakdownSortOption = { field: BreakdownSortField; label: string };
export type BreakdownFilterOption = { id: string; label: string };
