import type { PagedResult } from '@/shared/types/api';

import { normalizePagedResult } from './endpoints';
import type {
  DashboardLawFirmCaseReportRow,
  DashboardMedicalProviderReportRow,
  DashboardPiechart,
  DashboardStatResponse,
  DashboardTotalCaseReportRow,
  DashboardTotalLienReportRow,
} from './types';

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

function asNumber(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (typeof value === 'string') {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return 0;
}

export const LegacyCasesAdapter = {
  // Legacy shape: { isSuccess, message, data: { totalLiens, totalBillingAmount,
  // totalPurchaseAmount, liensByStatus: { [status]: { count, billingAmount,
  // purchaseAmount } }, totalCases, casesByStatus: { [status]: count },
  // totalLawFirmCases, lawFirmConcentration, totalMedicalFacilityCases,
  // facilityConcentration, startDate, endDate } }
  toDashboardPiechart(raw: unknown): DashboardPiechart {
    const data = asRecord(asRecord(raw).data);
    const casesByStatus = asRecord(data.casesByStatus);
    const liensByStatus = asRecord(data.liensByStatus);

    return {
      totalCases: asNumber(data.totalCases),
      totalActiveCases: asNumber(casesByStatus.Active),
      totalLiens: asNumber(data.totalLiens),
      totalLienValue: asNumber(data.totalBillingAmount),
      caseStatus: Object.entries(casesByStatus).map(([label, value]) => ({
        label,
        value: asNumber(value),
      })),
      lienStatus: Object.entries(liensByStatus).map(([label, entry]) => ({
        label,
        value: asNumber(asRecord(entry).count),
      })),
    };
  },

  // Legacy shape: { isSuccess, message, data: { deployedAmount | cashReceivedAmount, startDate, endDate } }
  toDashboardStatResponse(raw: unknown): DashboardStatResponse {
    const data = asRecord(asRecord(raw).data);
    const amount = data.deployedAmount ?? data.cashReceivedAmount;

    return {
      totalAmount: asNumber(amount),
      periodStart: typeof data.startDate === 'string' ? data.startDate : undefined,
      periodEnd: typeof data.endDate === 'string' ? data.endDate : undefined,
    };
  },

  // Legacy row: { lienId, caseId, plaintiffName, lienStatus }
  toTotalLienReportPage(raw: unknown): PagedResult<DashboardTotalLienReportRow> {
    const paged = normalizePagedResult<Record<string, unknown>>(raw);
    return {
      ...paged,
      items: paged.items as DashboardTotalLienReportRow[],
    };
  },

  // Legacy row: { caseId, plaintiffName, dateOfLoss, stage } — `stage` is renamed to
  // `status` since that's the first field the UI checks for case status.
  toTotalCaseReportPage(raw: unknown): PagedResult<DashboardTotalCaseReportRow> {
    const paged = normalizePagedResult<Record<string, unknown>>(raw);
    return {
      ...paged,
      items: paged.items.map((item) => ({
        ...item,
        status: item.status ?? item.stage,
      })) as DashboardTotalCaseReportRow[],
    };
  },

  // Legacy row: { caseId, plaintiffName, dateOfLoss, lawFirm } — matches as-is.
  toLawFirmCaseReportPage(raw: unknown): PagedResult<DashboardLawFirmCaseReportRow> {
    const paged = normalizePagedResult<Record<string, unknown>>(raw);
    return {
      ...paged,
      items: paged.items as DashboardLawFirmCaseReportRow[],
    };
  },

  // Legacy row: { caseId, plaintiffName, dateOfLoss, medicalFacility } — renamed to
  // `facilityName` since that's the first field the UI checks for facility name.
  toMedicalProviderReportPage(raw: unknown): PagedResult<DashboardMedicalProviderReportRow> {
    const paged = normalizePagedResult<Record<string, unknown>>(raw);
    return {
      ...paged,
      items: paged.items.map((item) => ({
        ...item,
        facilityName: item.facilityName ?? item.medicalFacility,
      })) as DashboardMedicalProviderReportRow[],
    };
  },
};
