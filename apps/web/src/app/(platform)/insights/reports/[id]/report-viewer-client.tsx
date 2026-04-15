'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { reportsService } from '@/lib/reports/reports.service';
import { DataGrid } from '@/components/reports/data-grid';
import { ExportModal } from '@/components/reports/export-modal';
import type {
  EffectiveReportDto,
  ReportExecutionResponse,
  ExportFormat,
  ColumnConfig,
} from '@/lib/reports/reports.types';

const MOCK_TENANT_ID = 'tenant-001';
const MOCK_USER_ID = 'user-001';

interface Props {
  templateId: string;
}

export function ReportViewerClient({ templateId }: Props) {
  const router = useRouter();
  const [report, setReport] = useState<EffectiveReportDto | null>(null);
  const [execution, setExecution] = useState<ReportExecutionResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [executing, setExecuting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [exportOpen, setExportOpen] = useState(false);
  const [filterValues, setFilterValues] = useState<Record<string, string>>({});

  const loadReport = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await reportsService.getEffectiveReport(templateId, MOCK_TENANT_ID);
      setReport(data);

      const filterConfig = reportsService.parseFilterConfig(data.effectiveFilterConfigJson);
      const initial: Record<string, string> = {};
      for (const f of filterConfig) {
        if (typeof f === 'object' && f !== null) {
          const key = (f as Record<string, unknown>).field ?? (f as Record<string, unknown>).name;
          if (typeof key === 'string') initial[key] = '';
        }
      }
      setFilterValues(initial);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load report');
    } finally {
      setLoading(false);
    }
  }, [templateId]);

  useEffect(() => { loadReport(); }, [loadReport]);

  async function handleRun() {
    setExecuting(true);
    setError(null);
    try {
      const params = Object.entries(filterValues).filter(([, v]) => v !== '');
      const result = await reportsService.executeReport({
        templateId,
        tenantId: MOCK_TENANT_ID,
        requestedByUserId: MOCK_USER_ID,
        filterParametersJson: params.length > 0 ? JSON.stringify(Object.fromEntries(params)) : undefined,
      });
      setExecution(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Execution failed');
    } finally {
      setExecuting(false);
    }
  }

  async function handleExport(format: ExportFormat) {
    await reportsService.exportReport({
      templateId,
      tenantId: MOCK_TENANT_ID,
      format,
      requestedByUserId: MOCK_USER_ID,
      filterParametersJson: Object.keys(filterValues).length > 0 ? JSON.stringify(filterValues) : undefined,
    });
  }

  const filterConfig = report ? reportsService.parseFilterConfig(report.effectiveFilterConfigJson) : [];

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-1">
          <button
            onClick={() => router.push('/insights/reports')}
            className="text-gray-400 hover:text-gray-600"
          >
            <i className="ri-arrow-left-line text-lg" />
          </button>
          <h1 className="text-xl font-bold text-gray-900">
            {loading ? 'Loading...' : report?.templateName ?? 'Report'}
          </h1>
        </div>
        {report?.templateDescription && (
          <p className="text-sm text-gray-500 ml-8 mb-6">{report.templateDescription}</p>
        )}

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {!loading && report && (
          <div className="space-y-6">
            {filterConfig.length > 0 && (
              <div className="bg-white border border-gray-200 rounded-lg p-4">
                <h3 className="text-sm font-semibold text-gray-700 mb-3">Filters</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  {filterConfig.map((f, i) => {
                    const rec = f as Record<string, unknown>;
                    const name = (rec.field ?? rec.name) as string;
                    const label = (rec.label ?? name) as string;
                    if (!name) return null;
                    return (
                      <div key={i}>
                        <label className="text-xs font-medium text-gray-600 mb-1 block">{label}</label>
                        <input
                          type="text"
                          value={filterValues[name] ?? ''}
                          onChange={(e) => setFilterValues((prev) => ({ ...prev, [name]: e.target.value }))}
                          className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm"
                          placeholder={`Enter ${label.toLowerCase()}`}
                        />
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            <div className="flex items-center gap-3">
              <button
                onClick={handleRun}
                disabled={executing}
                className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50 inline-flex items-center gap-2"
              >
                {executing ? (
                  <>
                    <i className="ri-loader-4-line animate-spin" />
                    Running...
                  </>
                ) : (
                  <>
                    <i className="ri-play-line" />
                    Run Report
                  </>
                )}
              </button>

              {execution && (
                <>
                  <button
                    onClick={() => setExportOpen(true)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 inline-flex items-center gap-2"
                  >
                    <i className="ri-download-2-line" />
                    Export
                  </button>
                  <button
                    onClick={() => router.push(`/insights/reports/${templateId}/builder`)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 inline-flex items-center gap-2"
                  >
                    <i className="ri-tools-line" />
                    Customize
                  </button>
                </>
              )}
            </div>

            {execution && (
              <div>
                <div className="flex items-center gap-4 mb-3">
                  <span className="text-xs text-gray-500">
                    {execution.totalRowCount} row{execution.totalRowCount !== 1 ? 's' : ''}
                  </span>
                  <span className="text-xs text-gray-400">
                    Executed in {execution.executionDurationMs}ms
                  </span>
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                    execution.status === 'Completed'
                      ? 'bg-green-100 text-green-700'
                      : 'bg-yellow-100 text-yellow-700'
                  }`}>
                    {execution.status}
                  </span>
                </div>
                <DataGrid columns={execution.columns} rows={execution.rows} />
              </div>
            )}
          </div>
        )}
      </div>

      <ExportModal
        open={exportOpen}
        onClose={() => setExportOpen(false)}
        onExport={handleExport}
        reportName={report?.templateName}
      />
    </div>
  );
}
