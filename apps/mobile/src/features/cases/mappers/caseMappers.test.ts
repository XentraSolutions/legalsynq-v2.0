import {
  buildCaseFilterOptions,
  filterCases,
  mapCaseReportRow,
} from './caseMappers';
import { EMPTY_CASE_FILTERS } from '../types/models';

describe('caseMappers', () => {
  const rows = [
    mapCaseReportRow(
      {
        caseId: 'case-1',
        caseNumber: 'CASE-001',
        clientDisplayName: 'Alex Rivera',
        status: 'Open',
        dateOfLoss: '2026-01-15',
        lawFirm: 'Rivera Law',
        lawFirmId: 'firm-1',
        accidentType: 'Auto Accident',
        accidentTypeId: 'auto',
        caseManager: 'Morgan Lee',
        caseManagerId: 'manager-1',
      },
      0
    ),
    mapCaseReportRow(
      {
        caseId: 'case-2',
        caseNumber: 'CASE-002',
        clientFirstName: 'Jordan',
        clientLastName: 'Kim',
        status: 'Closed',
        lawFirm: 'Kim Legal',
        lawFirmId: 'firm-2',
      },
      1
    ),
  ];

  it('normalizes report aliases into stable list fields', () => {
    const legacy = mapCaseReportRow(
      {
        caseId: 'legacy-1',
        status: 'Demand',
        dateOfLoss: '2025-03-10',
      },
      0
    );
    const raw = legacy as unknown as Record<string, string>;

    expect(raw.id).toBe('legacy-1');
    expect(legacy.status).toBe('Demand');
    expect(legacy.dateOfLoss).toBe('2025-03-10');
  });

  it('combines search with all applied filters', () => {
    const filtered = filterCases(rows, 'alex', {
      ...EMPTY_CASE_FILTERS,
      accidentTypeId: 'auto',
      caseManagerId: 'manager-1',
      lawFirmId: 'firm-1',
      statusId: 'Open',
    });

    expect(filtered.map((item) => item.id)).toEqual(['case-1']);
  });

  it('returns all rows when filters are omitted', () => {
    expect(filterCases(rows, '')).toEqual(rows);
  });

  it('builds deduplicated options from the complete report', () => {
    const options = buildCaseFilterOptions([...rows, rows[0]]);

    expect(options.statusId).toEqual([
      { id: 'Closed', label: 'Closed' },
      { id: 'Open', label: 'Open' },
    ]);
    expect(options.lawFirmId).toHaveLength(2);
  });
});
