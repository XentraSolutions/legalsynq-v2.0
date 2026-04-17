'use client';

import { useCallback, useEffect, useState } from 'react';
import { useCaseWorkflows } from '@/hooks/use-case-workflows';
import {
  workflowApi,
  type WorkflowApiAdapter,
  type WorkflowInstanceDetail,
} from '@/lib/workflow';
import { WorkflowEmptyState } from './workflow-empty-state';
import { WorkflowErrorState } from './workflow-error-state';
import { WorkflowLoadingState } from './workflow-loading-state';
import { WorkflowSummary } from './workflow-summary';
import { StartWorkflowAction } from './start-workflow-action';

export interface WorkflowPanelProps {
  /** Source-entity id (e.g. lien case id). */
  caseId: string;
  /** `synqlien` | `careconnect` | `synqfund`. */
  productKey: string;
  /** Friendly singular noun used in copy ("workflow", "care plan"…). */
  productLabel?: string;
  /** Frontend authorisation gates (the BFF remains the authoritative gate). */
  canView: boolean;
  canStart: boolean;
  /**
   * If `true` the panel additionally fetches the atomic ownership-aware
   * detail for the active workflow. Default: false (saves a roundtrip on
   * the SynqLien Details tab where the row data is enough).
   */
  loadDetail?: boolean;
  /**
   * BFF adapter. Defaults to the SynqLien adapter; CareConnect / SynqFund
   * pass their own adapter built via `createWorkflowApi(...)`.
   */
  api?: WorkflowApiAdapter;
}

/**
 * E8.1 — orchestrator component. Product-agnostic: wire it into any tenant
 * portal page by passing `caseId`, `productKey`, the role-access predicates,
 * and (for non-SynqLien products) the appropriate `api` adapter.
 */
export function WorkflowPanel({
  caseId,
  productKey,
  productLabel = 'workflow',
  canView,
  canStart,
  loadDetail = false,
  api = workflowApi,
}: WorkflowPanelProps) {
  const { loading, error, active, refresh } = useCaseWorkflows(canView ? caseId : undefined, api);
  const [detail, setDetail] = useState<WorkflowInstanceDetail | null>(null);
  const [detailError, setDetailError] = useState<Error | null>(null);

  // The A1 atomic GET is keyed on the Flow workflow-instance id (not the
  // product-workflow row id). We skip the call when the row hasn't been
  // bound to an instance yet (rare, but possible for freshly created rows
  // that haven't materialised a workflow instance).
  const detailKey = active?.workflowInstanceId ?? null;

  const loadActiveDetail = useCallback(async () => {
    if (!loadDetail || !detailKey) {
      setDetail(null);
      setDetailError(null);
      return;
    }
    try {
      const res = await api.getDetail(caseId, detailKey);
      setDetail(res.data ?? null);
      setDetailError(null);
    } catch (e) {
      setDetail(null);
      setDetailError(e instanceof Error ? e : new Error(String(e)));
    }
  }, [caseId, detailKey, loadDetail, api]);

  useEffect(() => { void loadActiveDetail(); }, [loadActiveDetail]);

  if (!canView) {
    return (
      <p className="text-xs text-gray-400 italic py-1">
        You don&apos;t have permission to view this {productLabel}.
      </p>
    );
  }

  if (loading) return <WorkflowLoadingState />;
  if (error)   return <WorkflowErrorState error={error} onRetry={() => void refresh()} />;

  if (!active) {
    return (
      <div className="space-y-2">
        <WorkflowEmptyState productLabel={productLabel} canStart={false} />
        {canStart && (
          <StartWorkflowAction
            caseId={caseId}
            productKey={productKey}
            api={api}
            onStarted={async () => { await refresh(); }}
          />
        )}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <WorkflowSummary row={active} detail={detail} />
      {detailError && <WorkflowErrorState error={detailError} onRetry={() => void loadActiveDetail()} />}
      {canStart && (
        <div className="pt-2 border-t border-gray-100">
          <StartWorkflowAction
            caseId={caseId}
            productKey={productKey}
            api={api}
            buttonLabel="Start another"
            onStarted={async () => { await refresh(); }}
          />
        </div>
      )}
    </div>
  );
}
