import { CaseExportService } from '@/features/cases/services/caseExportService';
import type { CaseListItem } from '@/features/cases/types/types';
import { LiensApi } from '@/shared/api/endpoints/Liens';
import type { ManagementLien, ManagementLienDetails } from '@/shared/api/endpoints/Liens';

import {
  exportServicingCases,
  filterServicingCases,
  loadServicingCases,
} from './servicingCaseService';

jest.mock('@/features/cases/services/caseExportService', () => ({
  CaseExportService: { share: jest.fn().mockResolvedValue(undefined) },
}));

const cases = [
  {
    caseNumber: '24-18743',
    clientName: 'Marcus Delgado',
    id: 'case-1',
    lawFirm: 'Morrison & Patel LLP',
    status: 'PreDemand',
  },
] as CaseListItem[];

function lien(id: string): ManagementLien {
  return {
    caseId: 'case-1',
    createdAtUtc: '2026-01-01T00:00:00Z',
    id,
    isConfidential: false,
    lienNumber: id,
    lienType: 'Medical',
    orgId: 'org-1',
    originalAmount: 500,
    status: 'Open',
    updatedAtUtc: '2026-01-01T00:00:00Z',
  };
}

function details(isServicing: boolean, purchase: string, billing: string): ManagementLienDetails {
  return {
    codeList: [
      {
        billingAmount: billing,
        code: 'A100',
        id: `code-${purchase}`,
        liensId: 'lien-1',
        medicareCost: '0',
        purchaseAmount: purchase,
      },
    ],
    documentList: [],
    facilityList: [],
    medicalList: [
      {
        caseId: 'case-1',
        endServiceDate: '',
        fundingCompany: '',
        fundingCompanyId: '',
        id: 'medical-1',
        initialServiceDate: '',
        isBulk: 'false',
        isServicing: String(isServicing),
        note: '',
        purchaseDate: '',
        status: 'Open',
      },
    ],
  };
}

describe('servicingCaseService', () => {
  beforeEach(() => jest.clearAllMocks());

  it('keeps servicing-enabled liens and aggregates their amounts by case', async () => {
    LiensApi.listAllManagementLiens = jest
      .fn()
      .mockResolvedValue([lien('lien-1'), lien('lien-2'), lien('lien-3')]);
    LiensApi.getManagementLienDetails = jest
      .fn()
      .mockResolvedValueOnce(details(true, '1000', '2500'))
      .mockResolvedValueOnce(details(true, '$500.50', '1,250'))
      .mockResolvedValueOnce(details(false, '9999', '9999'));

    await expect(loadServicingCases(cases)).resolves.toEqual([
      {
        billingAmount: 3750,
        caseId: 'case-1',
        caseNumber: '24-18743',
        clientName: 'Marcus Delgado',
        lawFirm: 'Morrison & Patel LLP',
        purchaseAmount: 1500.5,
        status: 'PreDemand',
      },
    ]);
  });

  it('searches servicing cases and exports the visible rows as CSV', async () => {
    const rows = [
      {
        billingAmount: 2500,
        caseId: 'case-1',
        caseNumber: '24-18743',
        clientName: 'Marcus Delgado',
        lawFirm: 'Morrison, Patel & Co.',
        purchaseAmount: 1000,
        status: 'PreDemand',
      },
    ];

    expect(filterServicingCases(rows, '24-18743')).toEqual(rows);
    expect(filterServicingCases(rows, 'not found')).toEqual([]);

    await exportServicingCases(rows);

    const file = (
      CaseExportService.share as unknown as {
        mock: { calls: Array<[{ base64: string; filename: string }]> };
      }
    ).mock.calls[0][0];
    const csv = Buffer.from(file.base64, 'base64').toString('utf8');
    expect(file.filename).toBe('Servicing-Cases.csv');
    expect(csv).toContain('Client,Case ID,Status,Law Firm,Purchase Amount,Billing Amount');
    expect(csv).toContain('Marcus Delgado,24-18743,PreDemand,"Morrison, Patel & Co."');
  });
});
