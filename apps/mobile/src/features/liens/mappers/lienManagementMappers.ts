import type { CaseListItem } from '@/features/cases/types/types';
import type {
  LienFormValues,
  LienManagementFilterOptions,
  LienManagementFilters,
  ManagementLienListItem,
} from '@/features/liens/types/types';
import type {
  LienFacility,
  ManagementLien,
  ManagementLienDetails,
} from '@/shared/api/endpoints/Liens';

function text(value: string | null | undefined): string {
  return value?.trim() ?? '';
}

function normalized(value: string): string {
  return value.trim().toLowerCase();
}

function withinDateRange(value: string, start: string, end: string): boolean {
  if (!start && !end) return true;
  if (!value) return false;
  const timestamp = new Date(value).getTime();
  if (Number.isNaN(timestamp)) return false;
  const startTime = start ? new Date(start).getTime() : Number.NEGATIVE_INFINITY;
  const endTime = end
    ? new Date(end.length === 10 ? `${end}T23:59:59.999` : end).getTime()
    : Number.POSITIVE_INFINITY;
  return timestamp >= startTime && timestamp <= endTime;
}

export function mapManagementLiens(
  liens: ManagementLien[],
  cases: CaseListItem[],
  facilities: LienFacility[]
): ManagementLienListItem[] {
  const casesById = new Map(cases.map((item) => [item.id, item]));
  const facilitiesById = new Map(facilities.map((item) => [item.id, item]));

  return liens.map((lien) => {
    const caseItem = lien.caseId ? casesById.get(lien.caseId) : undefined;
    const facility = lien.facilityId ? facilitiesById.get(lien.facilityId) : undefined;
    const patientName =
      text(lien.subjectDisplayName) ||
      [text(lien.subjectFirstName), text(lien.subjectLastName)].filter(Boolean).join(' ') ||
      caseItem?.clientName ||
      'Unnamed patient';

    return {
      id: lien.id,
      lienNumber: lien.lienNumber || 'N/A',
      patientName,
      status: lien.status || 'N/A',
      purchaseAmount: lien.purchasePrice ?? lien.originalAmount ?? 0,
      medicalFacility: facility?.name ?? '',
      medicalFacilityId: lien.facilityId ?? '',
      lawFirm: caseItem?.lawFirm ?? '',
      lawFirmId: caseItem?.lawFirmId ?? '',
      caseManager: caseItem?.caseManager ?? '',
      caseManagerId: caseItem?.caseManagerId ?? '',
      caseId: lien.caseId ?? '',
      purchaseDate: lien.incidentDate ?? '',
      closedDate: lien.closedAtUtc ?? '',
      initialServiceDate: '',
      billingAmount: 0,
    };
  });
}

export function filterManagementLiens(
  liens: ManagementLienListItem[],
  search: string,
  filters: LienManagementFilters
): ManagementLienListItem[] {
  const query = normalized(search);
  return liens.filter((lien) => {
    const matchesSearch =
      !query ||
      [
        lien.patientName,
        lien.lienNumber,
        lien.status,
        lien.medicalFacility,
        lien.lawFirm,
        lien.caseManager,
      ]
        .join(' ')
        .toLowerCase()
        .includes(query);

    return (
      matchesSearch &&
      (!filters.statusId || normalized(lien.status) === normalized(filters.statusId)) &&
      (!filters.medicalFacilityId || lien.medicalFacilityId === filters.medicalFacilityId) &&
      (!filters.lawFirmId || lien.lawFirmId === filters.lawFirmId) &&
      (!filters.caseManagerId || lien.caseManagerId === filters.caseManagerId) &&
      withinDateRange(lien.purchaseDate, filters.purchaseStartDate, filters.purchaseEndDate) &&
      withinDateRange(lien.closedDate, filters.closedStartDate, filters.closedEndDate)
    );
  });
}

function uniqueOptions(
  liens: ManagementLienListItem[],
  idKey: keyof ManagementLienListItem,
  labelKey: keyof ManagementLienListItem
) {
  const values = new Map<string, string>();
  liens.forEach((lien) => {
    const label = String(lien[labelKey] ?? '').trim();
    const id = String(lien[idKey] ?? '').trim() || label;
    if (id && label) values.set(id, label);
  });
  return Array.from(values, ([id, label]) => ({ id, label })).sort((a, b) =>
    a.label.localeCompare(b.label)
  );
}

export function buildLienFilterOptions(
  liens: ManagementLienListItem[]
): LienManagementFilterOptions {
  return {
    lawFirmId: uniqueOptions(liens, 'lawFirmId', 'lawFirm'),
    medicalFacilityId: uniqueOptions(liens, 'medicalFacilityId', 'medicalFacility'),
    caseManagerId: uniqueOptions(liens, 'caseManagerId', 'caseManager'),
    statusId: uniqueOptions(liens, 'status', 'status'),
  };
}

export function mapLienToForm(
  lien: ManagementLien,
  details: ManagementLienDetails
): LienFormValues {
  const medical = details.medicalList[0];
  const facility = details.facilityList[0];
  return {
    lienNumber: lien.lienNumber,
    caseId: medical?.caseId || lien.caseId || '',
    status: medical?.status || lien.status || 'Open',
    purchaseDate: medical?.purchaseDate || lien.incidentDate || '',
    initialServiceDate: medical?.initialServiceDate || '',
    endServiceDate: medical?.endServiceDate || '',
    notes: medical?.note || lien.description || '',
    isBulk: normalized(medical?.isBulk || '') === 'true',
    isServicing: normalized(medical?.isServicing || '') === 'true',
    fundingCompanyId: medical?.fundingCompanyId || lien.externalReference || '',
    facilityId: facility?.facilityId || lien.facilityId || '',
    facilityContactId: facility?.facilityContactId || '',
    facilityEmail: facility?.email || '',
    facilityPhone: facility?.phone || '',
    medicalProviderId: facility?.medicalProviderId || '',
    originalAmount: String(lien.originalAmount ?? ''),
    jurisdiction: lien.jurisdiction || '',
    subjectFirstName: lien.subjectFirstName || '',
    subjectLastName: lien.subjectLastName || '',
    medicalCodes: details.codeList.map((code) => ({
      id: code.id,
      code: code.code,
      medicalCost: code.medicareCost,
      billingAmount: code.billingAmount,
      purchaseAmount: code.purchaseAmount,
      payee: '',
      outboundCheckNumber: '',
    })),
    deletedMedicalCodeIds: [],
    payee: '',
    outboundCheckNumber: '',
  };
}
