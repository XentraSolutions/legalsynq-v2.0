import type { DashboardSummary } from '@/features/mockData';

export type DashboardReportType =
  | 'total-liens'
  | 'total-cases'
  | 'law-firm-allocation'
  | 'medical-facility-allocation';

export type DashboardDateRange = {
  startDate: string;
  endDate: string;
};

export type { DashboardSummary };
