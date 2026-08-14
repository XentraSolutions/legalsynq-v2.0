import type { CaseListItem } from '@/features/cases/types/types';
import type { LienFacility, ManagementLien } from '@/shared/api/endpoints/Liens';

import {
  buildLienFilterOptions,
  filterManagementLiens,
  mapManagementLiens,
} from './lienManagementMappers';
import { EMPTY_LIEN_MANAGEMENT_FILTERS } from '../types/types';

const lien: ManagementLien = {
  id: 'lien-1',
  lienNumber: 'LN-001',
  lienType: 'MedicalLien',
  status: 'Open',
  caseId: 'case-1',
  facilityId: 'facility-1',
  originalAmount: 10000,
  purchasePrice: 6500,
  isConfidential: false,
  subjectDisplayName: 'Alex Rivera',
  orgId: 'org-1',
  incidentDate: '2026-01-15',
  createdAtUtc: '2026-01-15T00:00:00Z',
  updatedAtUtc: '2026-01-15T00:00:00Z',
};

const caseItem: CaseListItem = {
  id: 'case-1',
  caseNumber: 'CASE-001',
  clientName: 'Alex Rivera',
  status: 'Open',
  dateOfLoss: '2026-01-10',
  lawFirm: 'Rivera Law',
  lawFirmId: 'firm-1',
  accidentType: 'Auto Accident',
  accidentTypeId: 'auto',
  caseManager: 'Morgan Lee',
  caseManagerId: 'manager-1',
  updatedAt: '2026-01-15',
};

const facility: LienFacility = {
  id: 'facility-1',
  name: 'Westside Orthopedic',
  isActive: true,
};

describe('lienManagementMappers', () => {
  const rows = mapManagementLiens([lien], [caseItem], [facility]);

  it('enriches lien records from existing case and facility data', () => {
    expect(rows[0]).toMatchObject({
      patientName: 'Alex Rivera',
      purchaseAmount: 6500,
      medicalFacility: 'Westside Orthopedic',
      lawFirm: 'Rivera Law',
      caseManager: 'Morgan Lee',
    });
  });

  it('combines search, relation filters, status, and date ranges', () => {
    const filtered = filterManagementLiens(rows, 'westside', {
      ...EMPTY_LIEN_MANAGEMENT_FILTERS,
      lawFirmId: 'firm-1',
      medicalFacilityId: 'facility-1',
      caseManagerId: 'manager-1',
      statusId: 'Open',
      purchaseStartDate: '2026-01-01',
      purchaseEndDate: '2026-01-31',
    });

    expect(filtered).toHaveLength(1);
  });

  it('builds filter options from normalized list values', () => {
    expect(buildLienFilterOptions(rows)).toMatchObject({
      lawFirmId: [{ id: 'firm-1', label: 'Rivera Law' }],
      medicalFacilityId: [{ id: 'facility-1', label: 'Westside Orthopedic' }],
      statusId: [{ id: 'Open', label: 'Open' }],
    });
  });
});
