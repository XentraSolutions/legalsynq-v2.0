import type { ReactNode } from 'react';
import { DashboardReportSkeleton } from '@/features/dashboard/components';
import { DashboardReportErrorCard } from './DashboardReportErrorCard';

export function DashboardReportState({
  children,
  hasSummaryRows,
  errorMessage,
  isDark,
  isError,
  isLoading,
  legendDetailRows,
  legendRows,
  onRetry,
  title,
}: {
  children: ReactNode;
  errorMessage?: string;
  hasSummaryRows?: boolean;
  isDark: boolean;
  isError: boolean;
  isLoading: boolean;
  legendDetailRows?: number;
  legendRows: number;
  onRetry: () => void;
  title: string;
}) {
  if (isLoading) {
    return (
      <DashboardReportSkeleton
        hasSummaryRows={hasSummaryRows}
        isDark={isDark}
        legendDetailRows={legendDetailRows}
        legendRows={legendRows}
      />
    );
  }

  if (isError) {
    return (
      <DashboardReportErrorCard
        isDark={isDark}
        message={errorMessage}
        title={title}
        onRetry={onRetry}
      />
    );
  }

  return children;
}
