/**
 * E8.1 — workflow API adapter. The shared workflow UI module talks to the
 * BFF through a small adapter interface so that CareConnect, SynqFund, and
 * any other product can wire the same components to their own product BFF
 * (`/api/care/...`, `/api/fund/...`, …) without forking the components or
 * the hooks.
 *
 * The default adapter targets SynqLien (`/lien/api/liens/...`).
 *
 * Every call routes through the product BFF which rewrites the platform
 * session cookie into a bearer header and forwards to the gateway. The
 * browser never talks to Flow directly.
 */
import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types';
import type {
  ProductWorkflowRow,
  StartWorkflowRequest,
  WorkflowDefinitionRow,
  WorkflowInstanceDetail,
} from './workflow.types';

export interface WorkflowApiAdapter {
  listForCase(caseId: string): Promise<ApiResponse<ProductWorkflowRow[]>>;
  start(caseId: string, body: StartWorkflowRequest): Promise<ApiResponse<ProductWorkflowRow>>;
  /**
   * Atomic ownership-aware GET (LS-FLOW-HARDEN-A1 surface). The route is
   * keyed on the **Flow workflow-instance id**, NOT the product-workflow
   * row id. Callers should pass `row.workflowInstanceId`.
   */
  getDetail(caseId: string, workflowInstanceId: string): Promise<ApiResponse<WorkflowInstanceDetail>>;
  listDefinitions(productKey: string): Promise<ApiResponse<WorkflowDefinitionRow[]>>;
}

export interface CreateWorkflowApiOptions {
  /** BFF path prefix, e.g. `/lien/api/liens` or `/care/api/care`. */
  basePath: string;
  /** Optional override of the case-scoped sub-path (default `/cases`). */
  caseSubPath?: string;
  /** Optional override of the definitions path (default `/workflow-definitions`). */
  definitionsSubPath?: string;
}

export function createWorkflowApi(opts: CreateWorkflowApiOptions): WorkflowApiAdapter {
  const caseSeg = opts.caseSubPath ?? '/cases';
  const defsSeg = opts.definitionsSubPath ?? '/workflow-definitions';

  const caseBase = (caseId: string) =>
    `${opts.basePath}${caseSeg}/${encodeURIComponent(caseId)}/workflows`;

  return {
    listForCase(caseId) {
      return apiClient.get<ProductWorkflowRow[]>(caseBase(caseId));
    },
    start(caseId, body) {
      return apiClient.post<ProductWorkflowRow>(caseBase(caseId), body);
    },
    getDetail(caseId, workflowInstanceId) {
      return apiClient.get<WorkflowInstanceDetail>(
        `${caseBase(caseId)}/${encodeURIComponent(workflowInstanceId)}`,
      );
    },
    listDefinitions(productKey) {
      return apiClient.get<WorkflowDefinitionRow[]>(
        `${opts.basePath}${defsSeg}?productKey=${encodeURIComponent(productKey)}`,
      );
    },
  };
}

/** Default adapter — SynqLien BFF. */
export const workflowApi: WorkflowApiAdapter = createWorkflowApi({
  basePath: '/lien/api/liens',
});
