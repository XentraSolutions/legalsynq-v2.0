export type CaseTrackingMetadata = {
  accidentType: string;
  caseDropped: boolean;
  childSupportLiens: boolean;
  currentMedicalStatus: string;
  documentType: string;
  isUccFiled: boolean;
  lead: string;
  leadId: string;
  minorComp: boolean;
  shareCase: boolean;
  stateOfIncident: string;
  trackingFollowUpDate: string;
};

const TRUE_VALUES = new Set(['1', 'true', 'yes', 'y']);

function parseBoolean(value?: string): boolean {
  return value ? TRUE_VALUES.has(value.trim().toLowerCase()) : false;
}

export function parseCaseTrackingMetadata(notes?: string | null): CaseTrackingMetadata {
  const fields = new Map<string, string>();

  for (const segment of notes?.split(';') ?? []) {
    const separator = segment.indexOf('=');
    if (separator <= 0) continue;
    fields.set(segment.slice(0, separator).trim().toLowerCase(), segment.slice(separator + 1).trim());
  }

  const get = (...keys: string[]): string => {
    for (const key of keys) {
      const value = fields.get(key.toLowerCase());
      if (value) return value;
    }
    return '';
  };

  return {
    accidentType: get('accidentType'),
    caseDropped: parseBoolean(get('caseDropped')),
    childSupportLiens: parseBoolean(get('childSupportLiens')),
    currentMedicalStatus: get('currentMedicalStatus'),
    documentType: get('documentType'),
    isUccFiled: parseBoolean(get('isUccFiled', 'uccFiled')),
    lead: get('lead', 'leadDescription'),
    leadId: get('leadId'),
    minorComp: parseBoolean(get('minorComp')),
    shareCase: parseBoolean(get('shareCase')),
    stateOfIncident: get('stateOfIncident', 'accidentState', 'state'),
    trackingFollowUpDate: get('trackingFollowUpDate'),
  };
}

export function mergeCaseTrackingMetadata(
  notes: string | null | undefined,
  updates: Partial<CaseTrackingMetadata>
): string {
  const fields = new Map<string, { key: string; value: string }>();
  const unstructuredSegments: string[] = [];

  for (const segment of notes?.split(';') ?? []) {
    const separator = segment.indexOf('=');
    if (separator <= 0) {
      if (segment.trim()) unstructuredSegments.push(segment.trim());
      continue;
    }
    const key = segment.slice(0, separator).trim();
    fields.set(key.toLowerCase(), { key, value: segment.slice(separator + 1).trim() });
  }

  const set = (key: string, value: string | boolean | undefined) => {
    if (value === undefined) return;
    const serialized = typeof value === 'boolean' ? String(value) : value.trim();
    if (!serialized) {
      fields.delete(key.toLowerCase());
      return;
    }
    fields.set(key.toLowerCase(), { key, value: serialized });
  };

  set('accidentType', updates.accidentType);
  set('caseDropped', updates.caseDropped);
  set('childSupportLiens', updates.childSupportLiens);
  set('currentMedicalStatus', updates.currentMedicalStatus);
  set('documentType', updates.documentType);
  set('isUccFiled', updates.isUccFiled);
  set('lead', updates.lead);
  set('leadId', updates.leadId);
  set('minorComp', updates.minorComp);
  set('shareCase', updates.shareCase);
  set('stateOfIncident', updates.stateOfIncident);
  set('trackingFollowUpDate', updates.trackingFollowUpDate);

  return [
    ...unstructuredSegments,
    ...Array.from(fields.values()).map(({ key, value }) => `${key}=${value}`),
  ].join('; ');
}
