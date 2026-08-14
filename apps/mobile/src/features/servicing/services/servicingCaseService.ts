import { Buffer } from 'buffer';

import { CaseExportService } from '@/features/cases/services/caseExportService';
import type { CaseListItem } from '@/features/cases/types/types';
import type { ServicingCaseListItem } from '@/features/servicing/types/types';
import { LiensApi } from '@/shared/api/endpoints/Liens';
import type { ManagementLien, ManagementLienDetails } from '@/shared/api/endpoints/Liens';

const DETAIL_BATCH_SIZE = 10;

function amount(value: string | number | null | undefined): number {
  if (typeof value === 'number') return Number.isFinite(value) ? value : 0;
  if (!value) return 0;
  const parsed = Number(value.replace(/[^0-9.-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

function isServicing(details: ManagementLienDetails): boolean {
  return details.medicalList.some((item) => item.isServicing.trim().toLowerCase() === 'true');
}

async function loadLienDetails(
  liens: ManagementLien[]
): Promise<Array<{ details: ManagementLienDetails; lien: ManagementLien }>> {
  const loaded: Array<{ details: ManagementLienDetails; lien: ManagementLien }> = [];

  for (let index = 0; index < liens.length; index += DETAIL_BATCH_SIZE) {
    const batch = liens.slice(index, index + DETAIL_BATCH_SIZE);
    const results = await Promise.allSettled(
      batch.map(async (lien) => ({
        details: await LiensApi.getManagementLienDetails(lien.id),
        lien,
      }))
    );
    results.forEach((result) => {
      if (result.status === 'fulfilled') loaded.push(result.value);
    });
  }

  if (liens.length > 0 && loaded.length === 0) {
    throw new Error('Servicing details could not be loaded.');
  }

  return loaded;
}

export async function loadServicingCases(cases: CaseListItem[]): Promise<ServicingCaseListItem[]> {
  const liens = await LiensApi.listAllManagementLiens();
  const details = await loadLienDetails(liens);
  const casesById = new Map(cases.map((item) => [item.id, item]));
  const grouped = new Map<string, ServicingCaseListItem>();

  details.forEach(({ details: lienDetails, lien }) => {
    if (!lien.caseId || !isServicing(lienDetails)) return;
    const caseItem = casesById.get(lien.caseId);
    if (!caseItem) return;

    const codePurchaseAmount = lienDetails.codeList.reduce(
      (total, code) => total + amount(code.purchaseAmount),
      0
    );
    const billingAmount = lienDetails.codeList.reduce(
      (total, code) => total + amount(code.billingAmount),
      0
    );
    const purchaseAmount =
      codePurchaseAmount || amount(lien.purchasePrice) || amount(lien.originalAmount);
    const current = grouped.get(caseItem.id);

    grouped.set(caseItem.id, {
      billingAmount: (current?.billingAmount ?? 0) + billingAmount,
      caseId: caseItem.id,
      caseNumber: caseItem.caseNumber,
      clientName: caseItem.clientName,
      lawFirm: caseItem.lawFirm,
      purchaseAmount: (current?.purchaseAmount ?? 0) + purchaseAmount,
      status: caseItem.status,
    });
  });

  return Array.from(grouped.values()).sort((left, right) =>
    left.clientName.localeCompare(right.clientName)
  );
}

export function filterServicingCases(
  cases: ServicingCaseListItem[],
  search: string
): ServicingCaseListItem[] {
  const query = search.trim().toLowerCase();
  if (!query) return cases;
  return cases.filter((item) =>
    [item.clientName, item.caseNumber, item.status, item.lawFirm]
      .join(' ')
      .toLowerCase()
      .includes(query)
  );
}

function csvCell(value: string | number): string {
  const text = String(value);
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

export async function exportServicingCases(cases: ServicingCaseListItem[]): Promise<void> {
  const rows = [
    ['Client', 'Case ID', 'Status', 'Law Firm', 'Purchase Amount', 'Billing Amount'],
    ...cases.map((item) => [
      item.clientName,
      item.caseNumber,
      item.status,
      item.lawFirm,
      item.purchaseAmount.toFixed(2),
      item.billingAmount.toFixed(2),
    ]),
  ];
  const csv = rows.map((row) => row.map(csvCell).join(',')).join('\n');

  await CaseExportService.share({
    base64: Buffer.from(csv, 'utf8').toString('base64'),
    export_format: 'csv',
    filename: 'Servicing-Cases.csv',
  });
}
