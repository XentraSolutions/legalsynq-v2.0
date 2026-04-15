'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { reportsService } from '@/lib/reports/reports.service';
import { ReportBuilder } from '@/components/reports/report-builder';
import type { ColumnConfig, FilterRule, EffectiveReportDto, FormulaDefinition, ColumnFormattingRule } from '@/lib/reports/reports.types';
import { useSessionContext } from '@/providers/session-provider';

interface Props {
  templateId: string;
}

export function ReportBuilderClient({ templateId }: Props) {
  const router = useRouter();
  const { session } = useSessionContext();
  const [report, setReport] = useState<EffectiveReportDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const tenantId = session?.tenantId ?? '';
  const userId = session?.userId ?? '';

  const load = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await reportsService.getEffectiveReport(templateId, tenantId);
      setReport(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load report');
    } finally {
      setLoading(false);
    }
  }, [templateId, tenantId]);

  useEffect(() => { load(); }, [load]);

  const availableFields: ColumnConfig[] = report
    ? reportsService.parseColumnConfig(report.effectiveColumnConfigJson)
    : [];

  const initialFilters: FilterRule[] = report
    ? (reportsService.parseFilterConfig(report.effectiveFilterConfigJson) as unknown as FilterRule[])
    : [];

  let initialFormulas: FormulaDefinition[] = [];
  try {
    if (report?.effectiveFormulaConfigJson) {
      initialFormulas = JSON.parse(report.effectiveFormulaConfigJson);
    }
  } catch { /* ignore */ }

  async function handleSave(columns: ColumnConfig[], filters: FilterRule[], formulas: FormulaDefinition[], formatting: ColumnFormattingRule[]) {
    await reportsService.createOverride({
      tenantId,
      templateId,
      baseTemplateVersionNumber: report?.publishedVersionNumber ?? 1,
      columnConfigJson: JSON.stringify(columns),
      filterConfigJson: JSON.stringify(filters),
      formulaConfigJson: formulas.length > 0 ? JSON.stringify(formulas) : undefined,
      createdByUserId: userId,
    });
    setSaved(true);
    setTimeout(() => router.push(`/insights/reports/${templateId}`), 1500);
  }

  async function handleSaveAsView(
    viewName: string,
    columns: ColumnConfig[],
    filters: FilterRule[],
    formulas: FormulaDefinition[],
    formatting: ColumnFormattingRule[],
    isDefault: boolean,
  ) {
    await reportsService.createView(templateId, {
      tenantId,
      reportTemplateId: templateId,
      baseTemplateVersionNumber: report?.publishedVersionNumber ?? 1,
      name: viewName,
      isDefault,
      columnConfigJson: JSON.stringify(columns),
      filterConfigJson: JSON.stringify(filters),
      formulaConfigJson: formulas.length > 0 ? JSON.stringify(formulas) : undefined,
      formattingConfigJson: formatting.length > 0 ? JSON.stringify(formatting) : undefined,
      createdByUserId: userId,
    });
    setSaved(true);
    setTimeout(() => router.push(`/insights/reports/${templateId}`), 1500);
  }

  if (loading) {
    return (
      <div className="min-h-full bg-gray-50 flex items-center justify-center py-20">
        <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
      </div>
    );
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex items-center gap-3 mb-6">
          <button
            onClick={() => router.push(`/insights/reports/${templateId}`)}
            className="text-gray-400 hover:text-gray-600"
          >
            <i className="ri-arrow-left-line text-lg" />
          </button>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Report Builder</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              {report?.templateName ?? 'Customize report columns, filters, formulas, and formatting'}
            </p>
          </div>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {saved && (
          <div className="bg-green-50 border border-green-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-green-700 font-medium">
              Configuration saved. Redirecting...
            </p>
          </div>
        )}

        {report && !saved && (
          <ReportBuilder
            availableFields={availableFields}
            initialColumns={availableFields.filter((f) => f.visible !== false)}
            initialFilters={initialFilters}
            initialFormulas={initialFormulas}
            onSave={handleSave}
            onSaveAsView={handleSaveAsView}
            onCancel={() => router.push(`/insights/reports/${templateId}`)}
          />
        )}
      </div>
    </div>
  );
}
