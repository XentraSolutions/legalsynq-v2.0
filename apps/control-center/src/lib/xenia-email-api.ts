const XENIA_BASE = process.env.XENIA_URL || 'http://127.0.0.1:5035';
const GATEWAY_BASE = process.env.CONTROL_CENTER_API_BASE || 'http://127.0.0.1:5010';

export interface EmailSource {
  id: string;
  tenantId: string;
  moduleKey: string;
  displayName: string;
  description?: string;
  providerType: string;
  authType: string;
  emailAddress: string;
  username?: string;
  incomingHost?: string;
  incomingPort?: number;
  useTls: boolean;
  mailboxFolder?: string;
  hasSecretReference: boolean;
  hasOAuthConnection: boolean;
  enabled: boolean;
  status: string;
  healthStatus: string;
  validationStatus: string;
  lastValidatedAt?: string;
  lastSuccessfulValidationAt?: string;
  lastValidationLatencyMs?: number;
  lastConnectionAt?: string;
  lastErrorCode?: string;
  lastErrorSummary?: string;
  rowVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface EmailProviderDefinition {
  providerKey: string;
  displayName: string;
  category: string;
  supportedAuthTypes: string[];
  defaultIncomingHost?: string;
  defaultPort?: number;
  requiresTls: boolean;
  supportsOAuth: boolean;
  supportsUsernamePassword: boolean;
  validationAvailable: boolean;
  helpText?: string;
}

export interface ValidationHistoryEntry {
  id: string;
  emailSourceId: string;
  providerType: string;
  validationType: string;
  startedAt: string;
  completedAt?: string;
  durationMs?: number;
  result: string;
  errorCode?: string;
  errorSummary?: string;
  createdAtUtc: string;
}

export interface EmailValidationResult {
  sourceId: string;
  success: boolean;
  result: string;
  durationMs: number;
  errorCode?: string;
  safeErrorSummary?: string;
  validatedAt: string;
}

export interface EmailModuleState {
  moduleKey: string;
  name: string;
  version: string;
  globalEnabled: boolean;
  tenantEnabled: boolean;
  effectiveEnabled: boolean;
  status: string;
}

export interface CreateEmailSourcePayload {
  displayName: string;
  description?: string;
  providerType: string;
  authType: string;
  emailAddress: string;
  username?: string;
  incomingHost?: string;
  incomingPort?: number;
  useTls: boolean;
  mailboxFolder?: string;
  secretReferenceId?: string;
  oauthConnectionRef?: string;
  enabled?: boolean;
  providerConfigurationJson?: string;
}

export interface UpdateEmailSourcePayload {
  displayName?: string;
  description?: string;
  username?: string;
  incomingHost?: string;
  incomingPort?: number;
  useTls?: boolean;
  mailboxFolder?: string;
  secretReferenceId?: string;
  oauthConnectionRef?: string;
  providerConfigurationJson?: string;
  expectedRowVersion?: number;
}

async function xeniaFetch(path: string, token: string, init?: RequestInit) {
  const res = await fetch(`${XENIA_BASE}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      ...(init?.headers as Record<string, string> | undefined),
    },
  });
  if (!res.ok) {
    const text = await res.text().catch(() => 'unknown error');
    throw new Error(`Xenia API ${path} → ${res.status}: ${text}`);
  }
  return res.json();
}

export async function getEmailModuleState(token: string): Promise<EmailModuleState> {
  return xeniaFetch('/email/module', token);
}

export async function enableEmailModule(token: string): Promise<void> {
  await xeniaFetch('/email/module/enable', token, { method: 'PUT' });
}

export async function disableEmailModule(token: string): Promise<void> {
  await xeniaFetch('/email/module/disable', token, { method: 'PUT' });
}

export async function getEmailSources(token: string): Promise<{ sources: EmailSource[]; total: number }> {
  return xeniaFetch('/email/sources', token);
}

export async function getEmailSource(token: string, id: string): Promise<EmailSource> {
  return xeniaFetch(`/email/sources/${id}`, token);
}

export async function createEmailSource(
  token: string,
  payload: CreateEmailSourcePayload,
): Promise<EmailSource> {
  return xeniaFetch('/email/sources', token, {
    method: 'POST',
    body: JSON.stringify(payload),
  });
}

export async function updateEmailSource(
  token: string,
  id: string,
  payload: UpdateEmailSourcePayload,
): Promise<EmailSource> {
  return xeniaFetch(`/email/sources/${id}`, token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });
}

export async function deleteEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/email/sources/${id}`, token, { method: 'DELETE' });
}

export async function enableEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/email/sources/${id}/enable`, token, { method: 'PUT' });
}

export async function disableEmailSource(token: string, id: string): Promise<void> {
  await xeniaFetch(`/email/sources/${id}/disable`, token, { method: 'PUT' });
}

export async function validateEmailSource(token: string, id: string): Promise<EmailValidationResult> {
  return xeniaFetch(`/email/sources/${id}/validate`, token, { method: 'POST' });
}

export async function getValidationHistory(
  token: string,
  id: string,
  limit = 20,
): Promise<{ source_id: string; history: ValidationHistoryEntry[]; total: number }> {
  return xeniaFetch(`/email/sources/${id}/validation-history?limit=${limit}`, token);
}

export async function getEmailProviders(
  token: string,
): Promise<{ providers: EmailProviderDefinition[]; total: number }> {
  return xeniaFetch('/email/providers', token);
}

export async function getEmailProvider(token: string, key: string): Promise<EmailProviderDefinition> {
  return xeniaFetch(`/email/providers/${key}`, token);
}

// ── Email Settings ────────────────────────────────────────────────────────────

export interface EmailSettings {
  id: string;
  tenantId: string;
  connectionTimeoutSeconds: number;
  allowedProviderTypes: string;
  validationRetryLimit: number;
  validationHistoryRetentionDays: number;
  allowedPorts: string;
  requireTls: boolean;
  allowCustomHosts: boolean;
  ssrfPolicyMode: string;
  defaultSourceEnabled: boolean;
  version: number;
  updatedAtUtc: string;
}

export interface UpdateEmailSettingsPayload {
  connectionTimeoutSeconds?: number;
  allowedProviderTypes?: string;
  validationRetryLimit?: number;
  validationHistoryRetentionDays?: number;
  allowedPorts?: string;
  requireTls?: boolean;
  allowCustomHosts?: boolean;
  ssrfPolicyMode?: string;
  defaultSourceEnabled?: boolean;
  expectedVersion: number;
}

export async function getEmailSettings(token: string): Promise<EmailSettings> {
  return xeniaFetch('/email/settings', token);
}

export async function updateEmailSettings(
  token: string,
  payload: UpdateEmailSettingsPayload,
): Promise<EmailSettings> {
  return xeniaFetch('/email/settings', token, {
    method: 'PUT',
    body: JSON.stringify(payload),
  });
}

// ── Email Sync / Ingestion ────────────────────────────────────────────────────

export interface EmailSyncState {
  id: string;
  tenantId: string;
  emailSourceId: string;
  providerType: string;
  cursorType: string;
  cursorValue?: string;
  safeCursorSummary?: string;
  lastSuccessfulSyncAt?: string;
  lastAttemptedSyncAt?: string;
  initialSyncCompleted: boolean;
  consecutiveFailureCount: number;
  nextEligibleSyncAt?: string;
  lastErrorCode?: string;
  safeLastErrorSummary?: string;
  stateVersion: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface IngestionRun {
  id: string;
  tenantId: string;
  emailSourceId: string;
  triggerType: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  durationMs?: number;
  correlationId?: string;
  messagesDiscovered: number;
  messagesImported: number;
  messagesUpdated: number;
  messagesDuplicated: number;
  messagesFailed: number;
  attachmentsDiscovered: number;
  attachmentsDispatched: number;
  attachmentsFailed: number;
  pagesProcessed: number;
  retryCount: number;
  cursorBeforeSafeSummary?: string;
  cursorAfterSafeSummary?: string;
  errorCode?: string;
  safeErrorSummary?: string;
  createdAtUtc: string;
}

export interface EmailMessageSummary {
  id: string;
  tenantId: string;
  emailSourceId: string;
  subject?: string;
  fromAddress?: string;
  fromName?: string;
  receivedAt?: string;
  sentAt?: string;
  importance: string;
  hasAttachments: boolean;
  attachmentCount: number;
  bodyPreview?: string;
  importStatus: string;
  importedAt?: string;
}

export interface EmailMessageAttachment {
  id: string;
  fileName: string;
  mimeType?: string;
  sizeBytes?: number;
  isInline: boolean;
  contentId?: string;
  dispatchStatus: string;
  documentReferenceId?: string;
}

export interface EmailMessageRecipient {
  id: string;
  recipientType: string;
  emailAddress: string;
  displayName?: string;
}

export interface EmailMessageDetail extends EmailMessageSummary {
  internetMessageId?: string;
  threadId?: string;
  conversationId?: string;
  senderAddress?: string;
  senderName?: string;
  replyToAddresses?: string;
  isRead?: boolean;
  bodyType: string;
  bodyText?: string;
  bodyPreview?: string;
  updatedAtUtc: string;
  recipients: EmailMessageRecipient[];
  attachments: EmailMessageAttachment[];
}

export interface EmailMessagesQuery {
  sourceId?: string;
  fromAddress?: string;
  subject?: string;
  importStatus?: string;
  pageSize?: number;
  pageOffset?: number;
}

export async function triggerEmailSync(token: string, sourceId: string): Promise<IngestionRun> {
  return xeniaFetch(`/email/sources/${sourceId}/sync`, token, { method: 'POST' });
}

export async function getEmailSyncState(
  token: string,
  sourceId: string,
): Promise<EmailSyncState> {
  return xeniaFetch(`/email/sources/${sourceId}/sync/state`, token);
}

export async function getIngestionHistory(
  token: string,
  sourceId: string,
  limit = 20,
): Promise<{ sourceId: string; runs: IngestionRun[]; total: number }> {
  return xeniaFetch(`/email/sources/${sourceId}/sync/history?limit=${limit}`, token);
}

export async function getEmailMessages(
  token: string,
  query: EmailMessagesQuery = {},
): Promise<{ messages: EmailMessageSummary[]; totalCount: number }> {
  const params = new URLSearchParams();
  if (query.sourceId)     params.set('sourceId',     query.sourceId);
  if (query.fromAddress)  params.set('fromAddress',  query.fromAddress);
  if (query.subject)      params.set('subject',      query.subject);
  if (query.importStatus) params.set('importStatus', query.importStatus);
  if (query.pageSize)     params.set('pageSize',     String(query.pageSize));
  if (query.pageOffset)   params.set('pageOffset',   String(query.pageOffset));
  const qs = params.toString();
  return xeniaFetch(`/email/messages${qs ? `?${qs}` : ''}`, token);
}

export async function getEmailMessage(
  token: string,
  messageId: string,
): Promise<EmailMessageDetail> {
  return xeniaFetch(`/email/messages/${messageId}`, token);
}

// ─── T4: Operations, Monitoring, and Alerts ───────────────────────────────────

export interface OperationsSummary {
  tenantId: string;
  from?: string;
  to?: string;
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  runningRuns: number;
  cancelledRuns: number;
  totalMessagesImported: number;
  totalErrors: number;
  openAlerts: number;
  criticalAlerts: number;
  warningAlerts: number;
  sourcesHealthy: number;
  sourcesDegraded: number;
  sourcesUnavailable: number;
  sourcesUnknown: number;
  lockContention: number;
  averageRunDurationMs?: number;
  lastRunAt?: string;
}

export interface SourceHealthSnapshot {
  sourceId: string;
  displayName: string;
  emailAddress: string;
  providerType: string;
  healthStatus: string;
  consecutiveFailureCount: number;
  nextEligibleSyncAt?: string;
  lastSuccessfulSyncAt?: string;
  lastAttemptedSyncAt?: string;
  lastErrorCode?: string;
  safeLastErrorSummary?: string;
  activeLockOwner?: string;
  lockExpiresAt?: string;
  fencingToken?: number;
  renewalFailureCount?: number;
}

export interface ProviderHealthSnapshot {
  providerType: string;
  displayName: string;
  totalSources: number;
  healthySources: number;
  degradedSources: number;
  unavailableSources: number;
  recentSuccessRate?: number;
  lastActivityAt?: string;
}

export interface IngestionRunSummary {
  id: string;
  tenantId: string;
  emailSourceId: string;
  sourceDisplayName?: string;
  providerType: string;
  status: string;
  triggerType: string;
  startedAt: string;
  completedAt?: string;
  durationMs?: number;
  messagesImported: number;
  messagesDuplicate: number;
  messagesSkipped: number;
  errorCount: number;
  retryCount: number;
  retryOfRunId?: string;
  correlationId?: string;
}

export interface IngestionRunDetail extends IngestionRunSummary {
  pagesProcessed: number;
  cursorBeforeSafeSummary?: string;
  cursorAfterSafeSummary?: string;
  errorCode?: string;
  safeErrorSummary?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface RunListResult {
  runs: IngestionRunSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface OperationalAlert {
  id: string;
  tenantId: string;
  emailSourceId?: string;
  sourceDisplayName?: string;
  providerType?: string;
  alertType: string;
  severity: string;
  status: string;
  deduplicationKey: string;
  title: string;
  safeDescription: string;
  firstObservedAt: string;
  lastObservedAt: string;
  occurrenceCount: number;
  acknowledgedAt?: string;
  resolvedAt?: string;
  resolutionReason?: string;
  suppressedUntil?: string;
  isSuppressedNow: boolean;
  correlationId?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AlertListResult {
  alerts: OperationalAlert[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface EmailOperationalSettings {
  id: string;
  tenantId: string;
  defaultDashboardRangeDays: number;
  sourceFailureAlertThreshold: number;
  staleSyncThresholdMinutes: number;
  lockWarningThresholdMinutes: number;
  maximumRetryCount: number;
  cancellationTimeoutSeconds: number;
  metricsEnabled: boolean;
  notificationAlertsEnabled: boolean;
  defaultRunPageSize: number;
  defaultMessagePageSize: number;
  messageMetadataRetentionDays: number;
  messageBodyRetentionDays: number;
  ingestionRunRetentionDays: number;
  alertRetentionDays: number;
  purgeBatchSize: number;
  retentionDryRunDefault: boolean;
  legalHoldEnabled: boolean;
  retentionEnabled: boolean;
  updatedAt: string;
  updatedBy?: string;
}

export interface RetentionRun {
  id: string;
  tenantId: string;
  mode: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  messagesEligible: number;
  messagesDeleted: number;
  bodiesCleared: number;
  runsDeleted: number;
  alertsDeleted: number;
  attachmentReferencesDeleted: number;
  failures: number;
  safeErrorSummary?: string;
  correlationId?: string;
  actorId?: string;
  createdAt: string;
}

// ── Operations summary ────────────────────────────────────────────────────────

export async function getEmailOperationsSummary(
  token: string,
  params: { from?: string; to?: string; sourceId?: string } = {},
): Promise<OperationsSummary> {
  const qs = new URLSearchParams();
  if (params.from)     qs.set('from',     params.from);
  if (params.to)       qs.set('to',       params.to);
  if (params.sourceId) qs.set('sourceId', params.sourceId);
  return xeniaFetch(`/api/v1/email/operations/summary${qs.size ? `?${qs}` : ''}`, token);
}

// ── Source health ─────────────────────────────────────────────────────────────

export async function getAllSourceHealth(token: string): Promise<{ items: SourceHealthSnapshot[]; count: number }> {
  return xeniaFetch('/api/v1/email/operations/sources/health', token);
}

export async function getSourceHealth(token: string, sourceId: string): Promise<SourceHealthSnapshot> {
  return xeniaFetch(`/api/v1/email/operations/sources/${sourceId}/health`, token);
}

// ── Provider health ───────────────────────────────────────────────────────────

export async function getAllProviderHealth(token: string): Promise<{ items: ProviderHealthSnapshot[]; count: number }> {
  return xeniaFetch('/api/v1/email/operations/providers/health', token);
}

// ── Runs ──────────────────────────────────────────────────────────────────────

export interface RunListQuery {
  page?: number;
  pageSize?: number;
  sourceId?: string;
  status?: string;
  trigger?: string;
  hasErrors?: boolean;
  from?: string;
  to?: string;
  correlationId?: string;
}

export async function listEmailRuns(token: string, query: RunListQuery = {}): Promise<RunListResult> {
  const qs = new URLSearchParams();
  if (query.page)          qs.set('page',          String(query.page));
  if (query.pageSize)      qs.set('pageSize',       String(query.pageSize));
  if (query.sourceId)      qs.set('sourceId',       query.sourceId);
  if (query.status)        qs.set('status',         query.status);
  if (query.trigger)       qs.set('trigger',        query.trigger);
  if (query.hasErrors != null) qs.set('hasErrors',  String(query.hasErrors));
  if (query.from)          qs.set('from',           query.from);
  if (query.to)            qs.set('to',             query.to);
  if (query.correlationId) qs.set('correlationId',  query.correlationId);
  return xeniaFetch(`/api/v1/email/operations/runs?${qs}`, token);
}

export async function getEmailRun(token: string, runId: string): Promise<IngestionRunDetail> {
  return xeniaFetch(`/api/v1/email/operations/runs/${runId}`, token);
}

export async function retryEmailRun(token: string, runId: string): Promise<{ runId: string; message: string }> {
  return xeniaFetch(`/api/v1/email/operations/runs/${runId}/retry`, token, { method: 'POST', body: '{}' });
}

export async function cancelEmailRun(token: string, runId: string): Promise<{ state: string }> {
  return xeniaFetch(`/api/v1/email/operations/runs/${runId}/cancel`, token, { method: 'POST', body: '{}' });
}

// ── Alerts ────────────────────────────────────────────────────────────────────

export interface AlertListQuery {
  page?: number;
  pageSize?: number;
  status?: string;
  severity?: string;
  alertType?: string;
  sourceId?: string;
}

export async function listEmailAlerts(token: string, query: AlertListQuery = {}): Promise<AlertListResult> {
  const qs = new URLSearchParams();
  if (query.page)      qs.set('page',      String(query.page));
  if (query.pageSize)  qs.set('pageSize',  String(query.pageSize));
  if (query.status)    qs.set('status',    query.status);
  if (query.severity)  qs.set('severity',  query.severity);
  if (query.alertType) qs.set('alertType', query.alertType);
  if (query.sourceId)  qs.set('sourceId',  query.sourceId);
  return xeniaFetch(`/api/v1/email/operations/alerts?${qs}`, token);
}

export async function acknowledgeAlert(token: string, alertId: string): Promise<{ acknowledged: boolean }> {
  return xeniaFetch(`/api/v1/email/operations/alerts/${alertId}/acknowledge`, token, { method: 'POST', body: '{}' });
}

export async function resolveAlert(token: string, alertId: string, reason?: string): Promise<{ resolved: boolean }> {
  return xeniaFetch(`/api/v1/email/operations/alerts/${alertId}/resolve`, token, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

// ── Settings ──────────────────────────────────────────────────────────────────

export async function getEmailOperationalSettings(token: string): Promise<EmailOperationalSettings> {
  return xeniaFetch('/api/v1/email/operations/settings', token);
}

export async function updateEmailOperationalSettings(
  token: string,
  settings: Partial<EmailOperationalSettings>,
): Promise<EmailOperationalSettings> {
  return xeniaFetch('/api/v1/email/operations/settings', token, {
    method: 'PUT',
    body: JSON.stringify(settings),
  });
}

// ── Retention ─────────────────────────────────────────────────────────────────

export async function runEmailRetention(
  token: string,
  dryRun = true,
): Promise<RetentionRun> {
  return xeniaFetch('/api/v1/email/operations/retention/run', token, {
    method: 'POST',
    body: JSON.stringify({ dryRun }),
  });
}

export async function getRetentionHistory(
  token: string,
  limit = 20,
): Promise<{ items: RetentionRun[]; count: number }> {
  return xeniaFetch(`/api/v1/email/operations/retention/history?limit=${limit}`, token);
}
