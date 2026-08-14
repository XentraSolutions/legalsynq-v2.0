export type LookupCategory =
  | 'AccidentType'
  | 'CaseStatus'
  | 'CurrentAttributes'
  | 'DocumentCategory'
  | 'MedicalStatus'
  | 'State';

export interface LookupValue {
  id: string;
  category: string;
  code: string;
  name: string;
  description?: string | null;
  sortOrder: number;
  isActive: boolean;
  isSystem: boolean;
}
