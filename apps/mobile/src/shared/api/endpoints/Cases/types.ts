import type { z } from 'zod';

import type {
  addCaseNoteRequestSchema,
  caseDetailResponseSchema,
  caseDetailsUpdateRequestSchema,
  caseExportFileSchema,
  caseExportFilterSchema,
  caseQueryParamsSchema,
  caseSchema,
  caseStatusSchema,
  caseUpdateSchema,
  createCaseRequestSchema,
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
  payoffQuoteSchema,
  personalCaseUpdateRequestSchema,
  piechartStatusSchema,
  primaryCaseUpdateRequestSchema,
  reportFilterRequestSchema,
  updateCaseStatusRequestSchema,
} from './schemas';

export type CaseStatus = z.infer<typeof caseStatusSchema>;
export type Case = z.infer<typeof caseSchema>;
export type CaseQueryParams = z.infer<typeof caseQueryParamsSchema>;
export type Note = z.infer<typeof noteSchema>;
export type AddCaseNoteRequest = z.infer<typeof addCaseNoteRequestSchema>;
export type CaseDetailResponse = z.infer<typeof caseDetailResponseSchema>;
export type PayoffQuote = z.infer<typeof payoffQuoteSchema>;
export type PersonalCaseUpdateRequest = z.infer<typeof personalCaseUpdateRequestSchema>;
export type PrimaryCaseUpdateRequest = z.infer<typeof primaryCaseUpdateRequestSchema>;
export type CaseDetailsUpdateRequest = z.infer<typeof caseDetailsUpdateRequestSchema>;
export type CaseUpdate = z.infer<typeof caseUpdateSchema>;
export type CreateCaseRequest = z.infer<typeof createCaseRequestSchema>;
export type CaseExportFilter = z.infer<typeof caseExportFilterSchema>;
export type CaseExportFile = z.infer<typeof caseExportFileSchema>;
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
