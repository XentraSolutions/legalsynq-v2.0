import type { LienView } from '@/features/mockData';
import type { LienCaseType, LienStatus } from '@/shared/api/endpoints/Liens';

export interface LienFilter {
  id: string;
  label: string;
  caseType?: LienCaseType;
  status?: LienStatus;
  maxAmount?: number;
  minAmount?: number;
}

export type { LienView };
