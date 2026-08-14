import { apiClient } from '@/lib/api-client';

export interface IntakeArtifact {
  id: string;
  inboundEmailId?: string | null;
  manualIntakeSubmissionId?: string | null;
  artifactSourceType: string;
  artifactKey: string;
  artifactType: string;
  artifactRole: string;
  artifactOrdinal: number;
  originalFileName: string;
  effectiveFileName: string;
  declaredContentType: string;
  detectedContentType?: string | null;
  sizeBytes: number;
  sha256?: string | null;
  processingStatus: string;
  failureCode?: string | null;
  failureMessage?: string | null;
  isRetryable: boolean;
  attemptCount: number;
  documentsServiceDocumentId?: string | null;
  documentsServiceVersionId?: string | null;
  documentsServiceReference?: string | null;
  uploadedAt?: string | null;
  completedAt?: string | null;
  updatedAt: string;
}

export interface ManualIntakeSubmission {
  id: string;
  tenantId: string;
  orgId?: string | null;
  tenantIntakeSourceId?: string | null;
  sourceType: string;
  purpose: string;
  processingProfileCode: string;
  title?: string | null;
  externalReference?: string | null;
  notes?: string | null;
  clientRequestId?: string | null;
  submittedBy?: string | null;
  submittedAt: string;
  status: string;
  failureMessage?: string | null;
  configurationVersion: number;
  profileConfigurationVersion: number;
  version: number;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
  artifacts: IntakeArtifact[];
}

export interface ManualIntakeListResponse {
  items: ManualIntakeSubmission[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface IntakeSource {
  sourceId: string;
  tenantId: string;
  sourceType: string;
  emailAddress: string;
  provider: string;
  purpose: string;
  processingProfileCode: string;
  isActive: boolean;
  isDefault: boolean;
  validationStatus: string;
  configurationVersion: number;
  lastValidatedAt?: string | null;
  lastValidationMessage?: string | null;
}

export interface IntakeSourceCode {
  code: string;
  displayName: string;
}

export async function listManualIntake(
  status?: string,
): Promise<ManualIntakeListResponse> {
  const query = status ? `?status=${encodeURIComponent(status)}` : '';
  return (await apiClient.get<ManualIntakeListResponse>(`/intake/manual-intake${query}`)).data;
}

export async function getManualIntake(id: string): Promise<ManualIntakeSubmission> {
  return (await apiClient.get<ManualIntakeSubmission>(`/intake/manual-intake/${id}`)).data;
}

export async function submitManualIntake(
  form: FormData,
): Promise<ManualIntakeSubmission> {
  return (await apiClient.postForm<ManualIntakeSubmission>('/intake/manual-intake', form)).data;
}

export async function retryManualArtifact(
  submissionId: string,
  artifactId: string,
  file: File,
): Promise<ManualIntakeSubmission> {
  const form = new FormData();
  form.append('file', file);
  return (
    await apiClient.postForm<ManualIntakeSubmission>(
      `/intake/manual-intake/${submissionId}/artifacts/${artifactId}/retry`,
      form,
    )
  ).data;
}

export async function cancelManualIntake(
  id: string,
  version: number,
): Promise<ManualIntakeSubmission> {
  return (
    await apiClient.post<ManualIntakeSubmission>(
      `/intake/manual-intake/${id}/cancel`,
      { version },
    )
  ).data;
}

export async function listIntakeSources(): Promise<IntakeSource[]> {
  return (await apiClient.get<IntakeSource[]>('/intake/sources')).data;
}

export async function listIntakeSourceTypes(): Promise<IntakeSourceCode[]> {
  return (await apiClient.get<IntakeSourceCode[]>('/intake/sources/types')).data;
}

export async function listIntakePurposes(): Promise<IntakeSourceCode[]> {
  return (await apiClient.get<IntakeSourceCode[]>('/intake/sources/purposes')).data;
}

export async function createIntakeSource(payload: unknown): Promise<IntakeSource> {
  return (await apiClient.post<IntakeSource>('/intake/sources', payload)).data;
}

export async function validateIntakeSource(
  sourceId: string,
  configurationVersion: number,
): Promise<unknown> {
  return (
    await apiClient.post(`/intake/sources/${sourceId}/validate`, {
      configurationVersion,
    })
  ).data;
}

export async function setIntakeSourceStatus(
  sourceId: string,
  isActive: boolean,
  configurationVersion: number,
): Promise<IntakeSource> {
  return (
    await apiClient.patch<IntakeSource>(`/intake/sources/${sourceId}/status`, {
      isActive,
      configurationVersion,
    })
  ).data;
}

// ── B12 Intake Center / Human Review ──────────────────────────────────────────

export interface IntakeReviewSummary {
  id: string;
  artifactId: string;
  artifactPolicyEvaluationId: string;
  status: string;
  priority: string;
  disposition: string;
  classificationCode: string;
  sourceType: string;
  createdAt: string;
  updatedAt: string;
  assignedToUserId?: string | null;
  version: number;
  isStale: boolean;
}

export interface IntakeReviewListResponse {
  items: IntakeReviewSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface IntakeReviewQueueSummary {
  pending: number;
  assigned: number;
  inReview: number;
  completedToday: number;
  highPriority: number;
  duplicateReviews: number;
  noMatchReviews: number;
  conflictedReviews: number;
  oldestPendingAt?: string | null;
}

export interface IntakeReviewResponse extends IntakeReviewSummary {
  tenantId: string;
  classificationId?: string | null;
  artifactExtractionId?: string | null;
  artifactNormalizationId?: string | null;
  artifactMatchRunId?: string | null;
  reviewOutcome: string;
  assignedAt?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  completedByUserId?: string | null;
  completionReasonCode?: string | null;
  completionComment?: string | null;
  revisionNumber: number;
}

export interface IntakeReviewFact {
  factCode: string;
  dataType: string;
  rawValue?: string | null;
  normalizedValue?: string | null;
  normalizedJson?: string | null;
  sourceConfidence: number;
  normalizationStatus: string;
  validationStatus: string;
  warningCodes: string[];
  evidenceReferences: string[];
  effectiveValue?: string | null;
  originalExtractedFactId?: string | null;
  originalNormalizedFactId?: string | null;
  correctionId?: string | null;
  sourceType: string;
  isHumanCorrected: boolean;
  isHumanAdded: boolean;
  isRejected: boolean;
}

export interface IntakeReviewMatch {
  id: string;
  entityType: string;
  candidateEntityId: string;
  displayLabel: string;
  score: number;
  rank: number;
  matchStatus: string;
  matchedFieldCount: number;
  conflictingFieldCount: number;
  fields: Array<{ factCode: string; outcome: string; reasonCode?: string | null }>;
}

export interface IntakeReviewWorkspace {
  review: IntakeReviewResponse;
  source: {
    sourceType: string;
    receivedAt?: string | null;
    emailSubject?: string | null;
    sender?: string | null;
    manualTitle?: string | null;
    manualReference?: string | null;
    documents: Array<{
      documentId?: string | null;
      artifactId: string;
      fileName: string;
      contentType: string;
      sizeBytes: number;
      reference?: string | null;
    }>;
  };
  classification?: {
    classificationCode?: string | null;
    classificationLabel?: string | null;
    confidence: number;
    reason?: string | null;
    wasOverridden: boolean;
    correctionId?: string | null;
    requiresReprocessing: boolean;
  } | null;
  facts: IntakeReviewFact[];
  matches: IntakeReviewMatch[];
  duplicates: IntakeReviewDuplicate[];
  findings: IntakeReviewFinding[];
  corrections: IntakeReviewCorrection[];
  matchDecisions: IntakeReviewMatchDecision[];
  duplicateDecisions: IntakeReviewDuplicateDecision[];
  findingDecisions: IntakeReviewFindingDecision[];
  activities: IntakeReviewActivity[];
}

export interface IntakeReviewDuplicate {
  id: string;
  duplicateType: string;
  relatedArtifactId?: string | null;
  relatedBusinessEntityId?: string | null;
  relatedBusinessEntityType?: string | null;
  score: number;
  status: string;
  reasonCode: string;
}

export interface IntakeReviewFinding {
  id: string;
  ruleCode: string;
  category: string;
  severity: string;
  outcome: string;
  reasonCode: string;
  entityType?: string | null;
  factCode?: string | null;
  score?: number | null;
  threshold?: number | null;
  evidenceReferences: string[];
  currentDecision?: string | null;
}

export interface IntakeReviewCorrection {
  id: string;
  factCode: string;
  correctionType: string;
  correctedValue?: string | null;
  normalizedValue?: string | null;
  validationStatus?: string | null;
  reasonCode: string;
  comment?: string | null;
  createdByUserId: string;
  createdAt: string;
  humanVerified: boolean;
}

export interface IntakeReviewMatchDecision {
  id: string;
  entityType: string;
  artifactEntityMatchId?: string | null;
  candidateEntityId?: string | null;
  decision: string;
  isManualSelection: boolean;
  reasonCode: string;
  createdByUserId: string;
  createdAt: string;
}

export interface IntakeReviewDuplicateDecision {
  id: string;
  artifactDuplicateSignalId: string;
  decision: string;
  reasonCode: string;
  createdByUserId: string;
  createdAt: string;
}

export interface IntakeReviewFindingDecision {
  id: string;
  artifactPolicyFindingId: string;
  decision: string;
  reasonCode: string;
  createdByUserId: string;
  createdAt: string;
}

export interface IntakeReviewActivity {
  id: string;
  activityType: string;
  actorUserId?: string | null;
  createdAt: string;
}

export async function listIntakeReviews(
  params: Record<string, string | number | boolean | undefined> = {},
): Promise<IntakeReviewListResponse> {
  const query = Object.entries(params)
    .filter(([, value]) => value !== undefined && value !== '' && value !== false)
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
    .join('&');
  return (await apiClient.get<IntakeReviewListResponse>(`/reviews${query ? `?${query}` : ''}`)).data;
}

export async function getIntakeReviewSummary(): Promise<IntakeReviewQueueSummary> {
  return (await apiClient.get<IntakeReviewQueueSummary>('/reviews/summary')).data;
}

export async function getIntakeReview(reviewId: string): Promise<IntakeReviewWorkspace> {
  return (await apiClient.get<IntakeReviewWorkspace>(`/reviews/${reviewId}`)).data;
}

export async function claimIntakeReview(reviewId: string, version: number): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(`/reviews/${reviewId}/claim`, { version })).data;
}

export async function unassignIntakeReview(reviewId: string, version: number): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(`/reviews/${reviewId}/unassign`, { version })).data;
}

export async function addReviewCorrection(
  reviewId: string,
  payload: {
    factCode: string;
    targetId?: string | null;
    correctionType: string;
    correctedValue?: string | null;
    dataType: string;
    reasonCode: string;
    comment?: string | null;
    humanVerified: boolean;
    version: number;
  },
): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(`/reviews/${reviewId}/corrections`, payload)).data;
}

export async function decideReviewMatch(
  reviewId: string,
  entityType: string,
  payload: {
    artifactEntityMatchId?: string | null;
    candidateEntityId?: string | null;
    decision: string;
    reasonCode: string;
    version: number;
  },
): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(
    `/reviews/${reviewId}/matches/${encodeURIComponent(entityType)}/decision`,
    payload,
  )).data;
}

export async function decideReviewDuplicate(
  reviewId: string,
  signalId: string,
  payload: { decision: string; reasonCode: string; version: number },
): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(
    `/reviews/${reviewId}/duplicates/${signalId}/decision`,
    payload,
  )).data;
}

export async function decideReviewFinding(
  reviewId: string,
  findingId: string,
  payload: { decision: string; reasonCode: string; version: number },
): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(
    `/reviews/${reviewId}/findings/${findingId}/decision`,
    payload,
  )).data;
}

export async function completeIntakeReview(
  reviewId: string,
  payload: {
    outcome: string;
    reasonCode?: string | null;
    comment?: string | null;
    version: number;
  },
): Promise<IntakeReviewResponse> {
  return (await apiClient.post<IntakeReviewResponse>(`/reviews/${reviewId}/complete`, payload)).data;
}