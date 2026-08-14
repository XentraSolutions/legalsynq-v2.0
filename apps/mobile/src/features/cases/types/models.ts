export type CaseFilters = {
  accidentTypeId: string;
  caseManagerId: string;
  lawFirmId: string;
  statusId: string;
};

export type CaseFilterKey = keyof CaseFilters;

export type CaseFilterOption = {
  id: string;
  label: string;
};

export type CaseListItem = {
  accidentType: string;
  accidentTypeId: string;
  caseManager: string;
  caseManagerId: string;
  caseNumber: string;
  clientName: string;
  dateOfLoss: string;
  id: string;
  lawFirm: string;
  lawFirmId: string;
  status: string;
  updatedAt: string;
};

export type CaseFilterOptions = Record<CaseFilterKey, CaseFilterOption[]>;

export const EMPTY_CASE_FILTERS: CaseFilters = {
  accidentTypeId: '',
  caseManagerId: '',
  lawFirmId: '',
  statusId: '',
};
