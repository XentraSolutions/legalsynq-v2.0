import type { DashboardTotalCaseReportRow } from '@/shared/api/endpoints/Cases';

import type {
  CaseFilterOptions,
  CaseFilters,
  CaseListItem,
} from '../types/models';
import { EMPTY_CASE_FILTERS } from '../types/models';

function readText(row: Record<string, unknown>, keys: string[]): string {
  for (const key of keys) {
    const value = row[key];
    if ((typeof value === 'string' || typeof value === 'number') && String(value).trim()) {
      return String(value).trim();
    }
  }
  return '';
}

export function mapCaseReportRow(
  row: DashboardTotalCaseReportRow,
  index: number
): CaseListItem {
  const raw = row as Record<string, unknown>;
  const firstName = readText(raw, ['clientFirstName', 'firstname', 'firstName']);
  const lastName = readText(raw, ['clientLastName', 'lastname', 'lastName']);
  const clientName =
    readText(raw, ['clientDisplayName', 'patientName', 'plaintiffName', 'plaintiff', 'name']) ||
    [firstName, lastName].filter(Boolean).join(' ') ||
    'Unnamed client';
  const caseNumber = readText(raw, [
    'caseNumber',
    'caseReference',
    'caseCode',
    'externalReference',
  ]);
  const lawFirm = readText(raw, ['lawFirm', 'lawfirm', 'lawFirmName', 'firmName']);
  const accidentType = readText(raw, ['accidentType', 'caseType']);
  const caseManager = readText(raw, ['caseManager', 'caseManagerName', 'assignedAttorney']);

  return {
    id: readText(raw, ['caseId', 'id']) || caseNumber || `case-report-${index}`,
    caseNumber: caseNumber || 'N/A',
    clientName,
    status: readText(raw, ['status', 'stage', 'caseStatus', 'currentStatus', 'statusName']) || 'N/A',
    dateOfLoss:
      readText(raw, ['dateOfLoss', 'dateOfIncident', 'incidentDate', 'lossDate']) || '',
    lawFirm,
    lawFirmId: readText(raw, ['lawFirmId', 'lawfirmId', 'lawFirmOrgId']) || lawFirm,
    accidentType,
    accidentTypeId: readText(raw, ['accidentTypeId', 'caseTypeId']) || accidentType,
    caseManager,
    caseManagerId: readText(raw, ['caseManagerId', 'assignedAttorneyId']) || caseManager,
    updatedAt: readText(raw, ['updatedAtUtc', 'updatedAt', 'createdAtUtc', 'createdAt']),
  };
}

function normalize(value: string): string {
  return value.trim().toLowerCase();
}

export function filterCases(
  cases: CaseListItem[] = [],
  search = '',
  filters: CaseFilters = EMPTY_CASE_FILTERS
): CaseListItem[] {
  const query = normalize(search);

  return cases.filter((caseItem) => {
    const matchesSearch =
      !query ||
      [
        caseItem.caseNumber,
        caseItem.clientName,
        caseItem.status,
        caseItem.lawFirm,
        caseItem.accidentType,
        caseItem.caseManager,
      ]
        .join(' ')
        .toLowerCase()
        .includes(query);

    return (
      matchesSearch &&
      (!filters.statusId || normalize(caseItem.status) === normalize(filters.statusId)) &&
      (!filters.lawFirmId || caseItem.lawFirmId === filters.lawFirmId) &&
      (!filters.accidentTypeId || caseItem.accidentTypeId === filters.accidentTypeId) &&
      (!filters.caseManagerId || caseItem.caseManagerId === filters.caseManagerId)
    );
  });
}

function uniqueOptions(
  cases: CaseListItem[],
  idKey: keyof CaseListItem,
  labelKey: keyof CaseListItem
) {
  const values = new Map<string, string>();
  cases.forEach((caseItem) => {
    const label = String(caseItem[labelKey] ?? '').trim();
    const id = String(caseItem[idKey] ?? '').trim() || label;
    if (id && label) values.set(id, label);
  });
  return Array.from(values, ([id, label]) => ({ id, label })).sort((left, right) =>
    left.label.localeCompare(right.label)
  );
}

export function buildCaseFilterOptions(cases: CaseListItem[]): CaseFilterOptions {
  const statusOptions = Array.from(new Set(cases.map((item) => item.status).filter(Boolean)))
    .sort((left, right) => left.localeCompare(right))
    .map((status) => ({ id: status, label: status }));

  return {
    statusId: statusOptions,
    lawFirmId: uniqueOptions(cases, 'lawFirmId', 'lawFirm'),
    accidentTypeId: uniqueOptions(cases, 'accidentTypeId', 'accidentType'),
    caseManagerId: uniqueOptions(cases, 'caseManagerId', 'caseManager'),
  };
}
