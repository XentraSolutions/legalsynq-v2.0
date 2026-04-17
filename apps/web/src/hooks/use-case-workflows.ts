/**
 * E8.1 — vanilla React data hooks for the workflow panel.
 * Mirrors the existing `casesService` pattern used by the rest of the
 * tenant portal (no React Query, no SWR).
 *
 * Each hook accepts an optional `WorkflowApiAdapter` so CareConnect /
 * SynqFund can inject their own product BFF adapter and reuse the same
 * hooks without duplication. Defaults to the SynqLien adapter.
 */
'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiError } from '@/lib/api-client';
import {
  workflowApi,
  pickActive,
  type ProductWorkflowRow,
  type StartWorkflowRequest,
  type WorkflowApiAdapter,
  type WorkflowDefinitionRow,
} from '@/lib/workflow';

export interface UseCaseWorkflowsResult {
  loading: boolean;
  error: ApiError | Error | null;
  rows: ProductWorkflowRow[];
  active: ProductWorkflowRow | null;
  refresh: () => Promise<void>;
}

export function useCaseWorkflows(
  caseId: string | undefined,
  api: WorkflowApiAdapter = workflowApi,
): UseCaseWorkflowsResult {
  const [rows, setRows] = useState<ProductWorkflowRow[]>([]);
  const [loading, setLoading] = useState<boolean>(Boolean(caseId));
  const [error, setError] = useState<ApiError | Error | null>(null);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const refresh = useCallback(async () => {
    if (!caseId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await api.listForCase(caseId);
      if (!mounted.current) return;
      setRows(res.data ?? []);
    } catch (e) {
      if (!mounted.current) return;
      setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [caseId, api]);

  useEffect(() => { void refresh(); }, [refresh]);

  return {
    loading,
    error,
    rows,
    active: pickActive(rows),
    refresh,
  };
}

export interface UseStartCaseWorkflowResult {
  starting: boolean;
  error: ApiError | Error | null;
  start: (body: StartWorkflowRequest) => Promise<ProductWorkflowRow | null>;
  reset: () => void;
}

export function useStartCaseWorkflow(
  caseId: string | undefined,
  onSuccess?: (row: ProductWorkflowRow) => void | Promise<void>,
  api: WorkflowApiAdapter = workflowApi,
): UseStartCaseWorkflowResult {
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<ApiError | Error | null>(null);

  const start = useCallback(async (body: StartWorkflowRequest) => {
    if (!caseId) return null;
    setStarting(true);
    setError(null);
    try {
      const res = await api.start(caseId, body);
      const row = res.data;
      if (row && onSuccess) await onSuccess(row);
      return row ?? null;
    } catch (e) {
      const err = e instanceof Error ? e : new Error(String(e));
      setError(err);
      return null;
    } finally {
      setStarting(false);
    }
  }, [caseId, onSuccess, api]);

  const reset = useCallback(() => setError(null), []);

  return { starting, error, start, reset };
}

export interface UseWorkflowDefinitionsResult {
  loading: boolean;
  error: ApiError | Error | null;
  definitions: WorkflowDefinitionRow[];
  refresh: () => Promise<void>;
}

export function useWorkflowDefinitions(
  productKey: string,
  enabled = true,
  api: WorkflowApiAdapter = workflowApi,
): UseWorkflowDefinitionsResult {
  const [definitions, setDefinitions] = useState<WorkflowDefinitionRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ApiError | Error | null>(null);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const refresh = useCallback(async () => {
    if (!enabled) return;
    setLoading(true);
    setError(null);
    try {
      const res = await api.listDefinitions(productKey);
      if (!mounted.current) return;
      setDefinitions(res.data ?? []);
    } catch (e) {
      if (!mounted.current) return;
      setError(e instanceof Error ? e : new Error(String(e)));
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [productKey, enabled, api]);

  useEffect(() => { void refresh(); }, [refresh]);

  return { loading, error, definitions, refresh };
}
