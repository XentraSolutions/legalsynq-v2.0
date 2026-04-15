export interface TemplateDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  productCode: string;
  organizationType: string;
  isActive: boolean;
  currentVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TemplateVersionDto {
  id: string;
  templateId: string;
  versionNumber: number;
  templateBody: string | null;
  outputFormat: string;
  changeNotes: string | null;
  isActive: boolean;
  isPublished: boolean;
  publishedAtUtc: string | null;
  createdAtUtc: string;
  createdByUserId: string;
}

export interface TemplateAssignmentDto {
  assignmentId: string;
  templateId: string;
  tenantId: string;
  assignedByUserId: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TenantCatalogItemDto {
  templateId: string;
  templateCode: string;
  templateName: string;
  templateDescription: string | null;
  productCode: string;
  organizationType: string;
  publishedVersionNumber: number;
  effectiveColumnConfigJson: string | null;
  effectiveFilterConfigJson: string | null;
  effectiveLayoutConfigJson: string | null;
  effectiveFormulaConfigJson: string | null;
  effectiveHeaderConfigJson: string | null;
  effectiveFooterConfigJson: string | null;
  isActive: boolean;
  requiredFeatureCode: string | null;
  minimumTierCode: string | null;
}

export interface ExecuteReportRequest {
  templateId: string;
  tenantId: string;
  versionNumber?: number;
  filterParametersJson?: string;
  requestedByUserId: string;
}

export interface ReportExecutionResponse {
  executionId: string;
  templateId: string;
  tenantId: string;
  status: string;
  columns: ReportColumnDto[];
  rows: ReportRowDto[];
  totalRowCount: number;
  executionDurationMs: number;
  executedAtUtc: string;
}

export interface ReportExecutionSummaryResponse {
  executionId: string;
  templateId: string;
  tenantId: string;
  status: string;
  totalRowCount: number;
  executionDurationMs: number;
  executedAtUtc: string;
  columns: ReportColumnDto[];
  rows: ReportRowDto[];
}

export interface ReportColumnDto {
  name: string;
  label: string;
  dataType: string;
  order: number;
}

export interface ReportRowDto {
  rowNumber: number;
  values: Record<string, unknown>;
}

export interface ExportReportRequest {
  templateId: string;
  tenantId: string;
  format: 'CSV' | 'XLSX' | 'PDF';
  filterParametersJson?: string;
  requestedByUserId: string;
}

export interface CreateScheduleRequest {
  templateId: string;
  tenantId: string;
  scheduleName: string;
  cronExpression: string;
  timezoneId: string;
  exportFormat: string;
  deliveryMethod: string;
  deliveryConfigJson?: string;
  filterParametersJson?: string;
  createdByUserId: string;
}

export interface UpdateScheduleRequest {
  scheduleName: string;
  cronExpression: string;
  timezoneId: string;
  exportFormat: string;
  deliveryMethod: string;
  deliveryConfigJson?: string;
  filterParametersJson?: string;
  updatedByUserId: string;
}

export interface ScheduleDto {
  scheduleId: string;
  templateId: string;
  tenantId: string;
  scheduleName: string;
  cronExpression: string;
  timezoneId: string;
  exportFormat: string;
  deliveryMethod: string;
  deliveryConfigJson: string | null;
  filterParametersJson: string | null;
  isActive: boolean;
  lastRunAtUtc: string | null;
  nextRunAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ScheduleRunDto {
  runId: string;
  scheduleId: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  executionDurationMs: number;
  exportFileSize: number | null;
  deliveryStatus: string | null;
  errorMessage: string | null;
}

export interface CreateOverrideRequest {
  tenantId: string;
  templateId: string;
  baseTemplateVersionNumber: number;
  nameOverride?: string;
  descriptionOverride?: string;
  layoutConfigJson?: string;
  columnConfigJson?: string;
  filterConfigJson?: string;
  formulaConfigJson?: string;
  headerConfigJson?: string;
  footerConfigJson?: string;
  createdByUserId: string;
}

export interface UpdateOverrideRequest {
  nameOverride?: string;
  descriptionOverride?: string;
  layoutConfigJson?: string;
  columnConfigJson?: string;
  filterConfigJson?: string;
  formulaConfigJson?: string;
  headerConfigJson?: string;
  footerConfigJson?: string;
  isActive?: boolean;
  updatedByUserId: string;
}

export interface OverrideDto {
  overrideId: string;
  tenantId: string;
  templateId: string;
  baseTemplateVersionNumber: number;
  nameOverride: string | null;
  descriptionOverride: string | null;
  layoutConfigJson: string | null;
  columnConfigJson: string | null;
  filterConfigJson: string | null;
  formulaConfigJson: string | null;
  headerConfigJson: string | null;
  footerConfigJson: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface EffectiveReportDto {
  templateId: string;
  templateCode: string;
  templateName: string;
  templateDescription: string | null;
  productCode: string;
  organizationType: string;
  publishedVersionNumber: number;
  effectiveColumnConfigJson: string | null;
  effectiveFilterConfigJson: string | null;
  effectiveLayoutConfigJson: string | null;
  effectiveFormulaConfigJson: string | null;
  effectiveHeaderConfigJson: string | null;
  effectiveFooterConfigJson: string | null;
  isActive: boolean;
  requiredFeatureCode: string | null;
  minimumTierCode: string | null;
}

export interface CreateTemplateRequest {
  code: string;
  name: string;
  description?: string;
  productCode: string;
  organizationType: string;
  isActive?: boolean;
}

export interface UpdateTemplateRequest {
  name: string;
  description?: string;
  productCode: string;
  organizationType: string;
  isActive?: boolean;
}

export interface CreateVersionRequest {
  templateBody: string;
  outputFormat: string;
  changeNotes?: string;
  isActive?: boolean;
  createdByUserId: string;
}

export interface PublishVersionRequest {
  publishedByUserId: string;
}

export interface CreateAssignmentRequest {
  tenantId: string;
  assignedByUserId: string;
  isActive?: boolean;
}

export interface UpdateAssignmentRequest {
  isActive: boolean;
  updatedByUserId: string;
}

export type ExportFormat = 'CSV' | 'XLSX' | 'PDF';
export type DeliveryMethod = 'OnScreen' | 'Email' | 'SFTP';

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ColumnConfig {
  name: string;
  label: string;
  dataType: string;
  order: number;
  visible: boolean;
}

export interface FilterRule {
  field: string;
  operator: 'equals' | 'contains' | 'greaterThan' | 'lessThan' | 'between' | 'in';
  value: string;
  value2?: string;
}
