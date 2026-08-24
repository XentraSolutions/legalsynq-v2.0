'use client';

import { useMemo } from 'react';
import type { ColumnDef, PaginationState } from '@tanstack/react-table';
import { Modal } from '@/components/lien/modal';
import { BaseTable } from '@/components/ui/base-table';
import { DonutChart } from './donut-chart';
import { tintColor } from './status-colors';
import type { ReportModalConfig } from './types';

export function ReportDetailModal({
  open,
  onClose,
  config,
  periodLabel,
  onExport,
  isExporting,
  page,
  pageSize,
  totalCount,
  onPageChange,
  isLoading,
}: {
  open: boolean;
  onClose: () => void;
  config: ReportModalConfig;
  periodLabel: string;
  /** Wire this to a backend export endpoint. Export button is disabled while unset. */
  onExport?: (config: ReportModalConfig) => void | Promise<void>;
  isExporting?: boolean;
  page?: number;
  pageSize?: number;
  totalCount?: number;
  onPageChange?: (page: number) => void;
  isLoading?: boolean;
}) {
  const filteredSegments = config.segments.filter((s) => s.value > 0);
  const grandTotal = filteredSegments.reduce((s, seg) => s + seg.value, 0);
  const tileSegments = filteredSegments.slice(0, 3);

  const columns = useMemo<ColumnDef<unknown, any>[]>(
    () =>
      config.columns.map((col, i) => ({
        id: `col-${i}`,
        header: col.label,
        enableSorting: false,
        cell: ({ row }: { row: { original: unknown } }) => col.render(row.original),
      })),
    [config.columns],
  );

  const handleExport = () => onExport?.(config);
  const serverPaginated =
    page !== undefined &&
    pageSize !== undefined &&
    totalCount !== undefined &&
    !!onPageChange;
  const pagination: PaginationState | undefined = serverPaginated
    ? { pageIndex: page - 1, pageSize }
    : undefined;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={config.title}
      subtitle={`Reporting Period: ${periodLabel}`}
      size="xl"
      footer={
        <>
          <button onClick={onClose} className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Close</button>
          <button
            onClick={handleExport}
            disabled={!onExport || isExporting}
            title={!onExport ? 'Export is not available yet' : undefined}
            className="flex items-center gap-1.5 text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <i className={`ri-download-2-line text-sm leading-none ${isExporting ? 'animate-pulse' : ''}`} />
            {isExporting ? 'Exporting...' : 'Export'}
          </button>
        </>
      }
    >
      <div className="flex flex-wrap gap-3 mb-6">
        <div className="min-w-35 flex-1 rounded-lg bg-emerald-50 px-4 py-3">
          <p className="text-xs font-medium text-emerald-700 mb-1">{config.totalLabel}</p>
          <p className="text-2xl font-bold text-emerald-600">{config.total.toLocaleString()}</p>
        </div>
        {tileSegments.map((seg) => (
          <div key={seg.label} className="min-w-35 flex-1 rounded-lg px-4 py-3" style={{ backgroundColor: tintColor(seg.color) }}>
            <p className="text-xs font-medium mb-1" style={{ color: seg.color }}>{seg.label}</p>
            <p className="text-2xl font-bold" style={{ color: seg.color }}>{seg.value.toLocaleString()}</p>
          </div>
        ))}
      </div>

      <h3 className="text-sm font-semibold text-gray-800 mb-3">Distribution</h3>
      <div className="flex items-start gap-8 mb-6">
        <DonutChart segments={filteredSegments.length > 0 ? filteredSegments : [{ label: 'None', value: 1, color: '#e5e7eb' }]} size={260} />
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-blue-600 mb-2">Legends:</p>
          <hr className="border-gray-100 mb-2" />
          <ul className="max-h-54 overflow-y-auto space-y-2 pr-2">
            {filteredSegments.map((seg) => (
              <li key={seg.label} className="flex items-center justify-between text-sm">
                <span className="flex items-center gap-2 min-w-0">
                  <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ backgroundColor: seg.color }} />
                  <span className="text-gray-700 truncate">{seg.label}</span>
                </span>
                <span className="text-gray-500 shrink-0 ml-3">{grandTotal > 0 ? ((seg.value / grandTotal) * 100).toFixed(2) : '0.00'}%</span>
              </li>
            ))}
          </ul>
        </div>
      </div>

      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-semibold text-gray-800">Detailed Breakdown</h3>
        <p className="text-xs text-gray-400">
          {(totalCount ?? config.rows.length).toLocaleString()} records
        </p>
      </div>
      <BaseTable
        key={config.title}
        data={config.rows}
        columns={columns}
        getRowId={(row) => String(config.rowKey(row))}
        emptyMessage="No records found"
        isLoading={isLoading}
        manualPagination={serverPaginated}
        pagination={pagination}
        pageSize={pageSize}
        pageCount={serverPaginated ? Math.ceil(totalCount / pageSize) : undefined}
        totalCount={totalCount}
        onPaginationChange={
          serverPaginated
            ? (updater) => {
                let next: PaginationState;
                if (typeof updater === 'function') {
                  if (!pagination) return;
                  next = updater(pagination);
                } else {
                  next = updater;
                }
                onPageChange(next.pageIndex + 1);
              }
            : undefined
        }
      />
    </Modal>
  );
}
