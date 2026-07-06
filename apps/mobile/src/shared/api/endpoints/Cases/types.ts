import type { z } from 'zod';

import type {
  addCaseNoteRequestSchema,
  caseQueryParamsSchema,
  caseSchema,
  caseStatusSchema,
  dashboardPiechartSchema,
  dashboardLawFirmCaseReportRowSchema,
  dashboardMedicalProviderReportRowSchema,
  dashboardStatRequestSchema,
  dashboardStatResponseSchema,
  dashboardTotalCaseReportRowSchema,
  dashboardTotalLienReportRowSchema,
  dashboardTaskSummarySchema,
  linkedLienSchema,
  noteSchema,
  piechartStatusSchema,
  reportFilterRequestSchema,
  updateCaseStatusRequestSchema,
} from './schemas';

export type CaseStatus = z.infer<typeof caseStatusSchema>;
export type Case = z.infer<typeof caseSchema>;
export type CaseQueryParams = z.infer<typeof caseQueryParamsSchema>;
export type Note = z.infer<typeof noteSchema>;
export type AddCaseNoteRequest = z.infer<typeof addCaseNoteRequestSchema>;
export type LinkedLien = z.infer<typeof linkedLienSchema>;
export type UpdateCaseStatusRequest = z.infer<typeof updateCaseStatusRequestSchema>;
export type PiechartStatus = z.infer<typeof piechartStatusSchema>;
export type DashboardPiechart = z.infer<typeof dashboardPiechartSchema>;
export type DashboardTotalLienReportRow = z.infer<typeof dashboardTotalLienReportRowSchema>;
export type DashboardTotalCaseReportRow = z.infer<typeof dashboardTotalCaseReportRowSchema>;
export type DashboardLawFirmCaseReportRow = z.infer<typeof dashboardLawFirmCaseReportRowSchema>;
export type DashboardMedicalProviderReportRow = z.infer<
  typeof dashboardMedicalProviderReportRowSchema
>;
export type DashboardTaskSummary = z.infer<typeof dashboardTaskSummarySchema>;
export type ReportFilterRequest = z.infer<typeof reportFilterRequestSchema>;
export type DashboardStatRequest = z.infer<typeof dashboardStatRequestSchema>;
export type DashboardStatResponse = z.infer<typeof dashboardStatResponseSchema>;
