import type { z } from 'zod';

import type {
  addCaseNoteRequestSchema,
  caseQueryParamsSchema,
  caseSchema,
  caseStatusSchema,
  linkedLienSchema,
  noteSchema,
  updateCaseStatusRequestSchema,
} from './schemas';

export type CaseStatus = z.infer<typeof caseStatusSchema>;
export type Case = z.infer<typeof caseSchema>;
export type CaseQueryParams = z.infer<typeof caseQueryParamsSchema>;
export type Note = z.infer<typeof noteSchema>;
export type AddCaseNoteRequest = z.infer<typeof addCaseNoteRequestSchema>;
export type LinkedLien = z.infer<typeof linkedLienSchema>;
export type UpdateCaseStatusRequest = z.infer<typeof updateCaseStatusRequestSchema>;
