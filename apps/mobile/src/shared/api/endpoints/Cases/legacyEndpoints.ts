import { LegacyPsaService } from '@/shared/services/LegacyPsa';

import type { DashboardStatRequest, ReportFilterRequest } from './types';

// Exact method identifiers required by the legacy Service/Request dispatcher for
// the "Case" service — casing/format (slash vs underscore) as given, not normalized.
const LEGACY_METHOD = {
  piechart: 'dashboard/piechart',
  deployed: 'dashboard_deployed',
  cashReceived: 'dashboard_cash-received',
  totalLienReport: 'dashboard_total-lien-report-export_v3',
  totalCaseReport: 'dashboard_total-case-report-export_v3',
  lawFirmCaseReport: 'dashboard_lawfirm-case-report-export_v3',
  medicalProviderReport: 'dashboard_medical-provider-report-export_v3',
} as const;

function toLegacyReportPage(body: ReportFilterRequest): { page: number; limit: number } {
  return {
    page: body.page ?? 1,
    limit: body.limit ?? 20,
  };
}

export const LegacyCasesApi = {
  async getDashboardPiechart(): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.piechart);
  },

  async getDashboardDeployed(body: DashboardStatRequest): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.deployed, {
      startDate: body.fromDate,
      endDate: body.toDate,
    });
  },

  async getDashboardCashReceived(body: DashboardStatRequest): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.cashReceived, {
      startDate: body.fromDate,
      endDate: body.toDate,
    });
  },

  async getDashboardTotalLienReportV3(body: ReportFilterRequest): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.totalLienReport, {
      ...toLegacyReportPage(body),
      lienId: '',
      keyword: '',
      startDate: body.startDate,
      endDate: body.endDate,
    });
  },

  async getDashboardTotalCaseReportV3(body: ReportFilterRequest): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.totalCaseReport, {
      ...toLegacyReportPage(body),
      keyword: '',
      startDate: body.startDate,
      endDate: body.endDate,
    });
  },

  async getDashboardLawFirmCaseReportV3(body: ReportFilterRequest): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.lawFirmCaseReport, {
      ...toLegacyReportPage(body),
      keyword: '',
      purchaseDateFrom: body.startDate,
      purchaseDateTo: body.endDate,
    });
  },

  async getDashboardMedicalProviderReportV3(body: ReportFilterRequest): Promise<unknown> {
    return LegacyPsaService.callCaseService(LEGACY_METHOD.medicalProviderReport, {
      ...toLegacyReportPage(body),
      keyword: '',
      purchaseDateFrom: body.startDate,
      purchaseDateTo: body.endDate,
    });
  },
};
