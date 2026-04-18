'use client';

import { useState, useEffect, useCallback } from 'react';
import { lienWorkflowApi } from '@/lib/liens/lien-workflow.api';
import type {
  WorkflowConfigDto,
  WorkflowStageDto,
  AddWorkflowStageRequest,
  UpdateWorkflowStageRequest,
} from '@/lib/liens/lien-workflow.types';

interface TenantWorkflowState {
  tenantId: string;
  tenantName: string;
  config: WorkflowConfigDto | null;
  loading: boolean;
  error: string | null;
}

type StageFormData = {
  stageName: string;
  stageOrder: number;
  description: string;
  defaultOwnerRole: string;
  isActive: boolean;
};

const DEFAULT_STAGE: StageFormData = {
  stageName: '',
  stageOrder: 1,
  description: '',
  defaultOwnerRole: '',
  isActive: true,
};

export default function ControlCenterLienWorkflowPage() {
  const [tenantId, setTenantId] = useState('');
  const [tenantName, setTenantName] = useState('');
  const [searched, setSearched] = useState(false);
  const [tenantState, setTenantState] = useState<TenantWorkflowState | null>(null);
  const [searching, setSearching] = useState(false);

  const [workflowName, setWorkflowName] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);

  const [showStageForm, setShowStageForm] = useState(false);
  const [editStage, setEditStage] = useState<WorkflowStageDto | null>(null);
  const [stageForm, setStageForm] = useState<StageFormData>(DEFAULT_STAGE);
  const [stageSaving, setStageSaving] = useState(false);

  async function handleSearch() {
    if (!tenantId.trim()) return;
    setSearching(true);
    setTenantState({ tenantId, tenantName: tenantName || tenantId, config: null, loading: true, error: null });
    setSearched(true);
    try {
      const config = await lienWorkflowApi.adminGet(tenantId);
      setTenantState({ tenantId, tenantName: tenantName || tenantId, config: config ?? null, loading: false, error: null });
      if (config) {
        setWorkflowName(config.workflowName);
        setIsActive(config.isActive);
      } else {
        setWorkflowName('');
        setIsActive(true);
      }
    } catch (err) {
      setTenantState({ tenantId, tenantName: tenantName || tenantId, config: null, loading: false, error: err instanceof Error ? err.message : 'Failed to load config' });
    } finally {
      setSearching(false);
    }
  }

  async function handleSaveConfig() {
    if (!tenantState) return;
    setSaving(true);
    try {
      let updated: WorkflowConfigDto;
      if (tenantState.config) {
        updated = await lienWorkflowApi.adminUpdate(tenantId, tenantState.config.id, {
          workflowName,
          isActive,
          updateSource: 'CONTROL_CENTER',
          version: tenantState.config.version,
        });
      } else {
        updated = await lienWorkflowApi.adminCreate(tenantId, {
          workflowName,
          updateSource: 'CONTROL_CENTER',
        });
      }
      setTenantState((prev) => prev ? { ...prev, config: updated } : null);
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to save');
    } finally {
      setSaving(false);
    }
  }

  function openAddStage() {
    setEditStage(null);
    const nextOrder = tenantState?.config ? Math.max(0, ...tenantState.config.stages.map((s) => s.stageOrder)) + 1 : 1;
    setStageForm({ ...DEFAULT_STAGE, stageOrder: nextOrder });
    setShowStageForm(true);
  }

  function openEditStage(stage: WorkflowStageDto) {
    setEditStage(stage);
    setStageForm({
      stageName:        stage.stageName,
      stageOrder:       stage.stageOrder,
      description:      stage.description ?? '',
      defaultOwnerRole: stage.defaultOwnerRole ?? '',
      isActive:         stage.isActive,
    });
    setShowStageForm(true);
  }

  async function handleSaveStage() {
    if (!tenantState?.config) return;
    setStageSaving(true);
    try {
      let updated: WorkflowConfigDto;
      if (editStage) {
        const req: UpdateWorkflowStageRequest = {
          stageName: stageForm.stageName, stageOrder: stageForm.stageOrder, isActive: stageForm.isActive,
          description: stageForm.description || undefined, defaultOwnerRole: stageForm.defaultOwnerRole || undefined,
        };
        updated = await lienWorkflowApi.adminUpdateStage(tenantId, tenantState.config.id, editStage.id, req);
      } else {
        const req: AddWorkflowStageRequest = {
          stageName: stageForm.stageName, stageOrder: stageForm.stageOrder,
          description: stageForm.description || undefined, defaultOwnerRole: stageForm.defaultOwnerRole || undefined,
        };
        updated = await lienWorkflowApi.adminAddStage(tenantId, tenantState.config.id, req);
      }
      setTenantState((prev) => prev ? { ...prev, config: updated } : null);
      setShowStageForm(false);
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to save stage');
    } finally {
      setStageSaving(false);
    }
  }

  async function handleRemoveStage(stageId: string) {
    if (!tenantState?.config) return;
    try {
      const updated = await lienWorkflowApi.adminRemoveStage(tenantId, tenantState.config.id, stageId);
      setTenantState((prev) => prev ? { ...prev, config: updated } : null);
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to remove stage');
    }
  }

  const config = tenantState?.config;
  const sortedStages = config?.stages.filter((s) => s.isActive).sort((a, b) => a.stageOrder - b.stageOrder) ?? [];

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Synq Liens — Workflow Configuration</h1>
        <p className="text-sm text-gray-500 mt-1">View and manage per-tenant workflow settings from Control Center</p>
      </div>

      {/* Tenant selector */}
      <div className="bg-white border border-gray-200 rounded-xl p-6">
        <h2 className="text-sm font-semibold text-gray-700 mb-4 flex items-center gap-2">
          <i className="ri-building-line text-blue-600" /> Select Tenant
        </h2>
        <div className="flex items-end gap-3">
          <div className="flex-1">
            <label className="block text-xs font-medium text-gray-500 mb-1">Tenant ID</label>
            <input
              type="text"
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30"
              placeholder="Enter tenant UUID..."
              onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            />
          </div>
          <div className="flex-1">
            <label className="block text-xs font-medium text-gray-500 mb-1">Tenant Name (optional)</label>
            <input
              type="text"
              value={tenantName}
              onChange={(e) => setTenantName(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30"
              placeholder="Display name..."
            />
          </div>
          <button
            onClick={handleSearch}
            disabled={!tenantId.trim() || searching}
            className="flex items-center gap-2 px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-60"
          >
            {searching ? <i className="ri-loader-4-line animate-spin" /> : <i className="ri-search-line" />}
            Load Config
          </button>
        </div>
      </div>

      {/* Config display */}
      {searched && tenantState && (
        <>
          {tenantState.loading && (
            <div className="text-center py-8 text-gray-400">
              <i className="ri-loader-4-line animate-spin text-2xl block mb-2" />
              Loading...
            </div>
          )}

          {tenantState.error && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
              {tenantState.error}
            </div>
          )}

          {!tenantState.loading && !tenantState.error && (
            <>
              {/* General config */}
              <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
                <div className="flex items-center justify-between">
                  <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
                    <i className="ri-git-branch-line text-blue-600" />
                    Workflow Config — <span className="font-normal text-gray-500">{tenantState.tenantName}</span>
                  </h2>
                  {config && (
                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${config.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                      {config.isActive ? 'Active' : 'Inactive'}
                    </span>
                  )}
                </div>

                {!config && (
                  <div className="bg-amber-50 border border-amber-200 rounded-lg px-4 py-3 text-sm text-amber-700 flex items-center gap-2">
                    <i className="ri-information-line" /> No workflow config exists for this tenant. You can create one below.
                  </div>
                )}

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-500 mb-1">Workflow Name</label>
                    <input
                      type="text"
                      value={workflowName}
                      onChange={(e) => setWorkflowName(e.target.value)}
                      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                      placeholder="e.g. Standard Lien Workflow"
                    />
                  </div>
                  <div className="flex items-end gap-4">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <div
                        className={`relative w-10 h-5 rounded-full transition-colors ${isActive ? 'bg-blue-600' : 'bg-gray-300'}`}
                        onClick={() => setIsActive((v) => !v)}
                      >
                        <div className={`absolute top-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform ${isActive ? 'translate-x-5' : 'translate-x-0.5'}`} />
                      </div>
                      <span className="text-sm text-gray-700">Active</span>
                    </label>
                  </div>
                </div>

                {config && (
                  <div className="text-xs text-gray-400 space-y-0.5">
                    <div>Version: <span className="font-medium text-gray-600">v{config.version}</span></div>
                    <div>Last updated: <span className="font-medium text-gray-600">{config.lastUpdatedByName ?? 'System'}</span> via <span className="font-medium text-gray-600">{config.lastUpdatedSource}</span></div>
                  </div>
                )}

                <div className="flex justify-end">
                  <button
                    onClick={handleSaveConfig}
                    disabled={saving || !workflowName.trim()}
                    className="flex items-center gap-2 px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-60"
                  >
                    {saving && <i className="ri-loader-4-line animate-spin" />}
                    {config ? 'Save Changes' : 'Create Workflow Config'}
                  </button>
                </div>
              </div>

              {/* Stages */}
              {config && (
                <div className="bg-white border border-gray-200 rounded-xl p-6 space-y-4">
                  <div className="flex items-center justify-between">
                    <h2 className="text-sm font-semibold text-gray-700 flex items-center gap-2">
                      <i className="ri-layout-column-line text-blue-600" /> Stages
                      <span className="text-xs bg-gray-100 text-gray-500 rounded-full px-2 py-0.5">{sortedStages.length}</span>
                    </h2>
                    <button onClick={openAddStage} className="flex items-center gap-1.5 text-sm text-blue-600 border border-blue-200 rounded-lg px-3 py-1.5 hover:bg-blue-50">
                      <i className="ri-add-line" /> Add Stage
                    </button>
                  </div>

                  {sortedStages.length === 0 ? (
                    <p className="text-sm text-gray-400 text-center py-6">No active stages.</p>
                  ) : (
                    <div className="space-y-2">
                      {sortedStages.map((stage) => (
                        <div key={stage.id} className="flex items-start gap-4 p-4 border border-gray-100 rounded-lg hover:bg-gray-50">
                          <div className="flex items-center justify-center w-7 h-7 rounded-full bg-blue-100 text-blue-700 text-xs font-bold shrink-0">
                            {stage.stageOrder}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-semibold text-gray-800">{stage.stageName}</span>
                              {stage.defaultOwnerRole && (
                                <span className="text-xs bg-blue-50 text-blue-700 rounded px-1.5 py-0.5">{stage.defaultOwnerRole}</span>
                              )}
                            </div>
                            {stage.description && <p className="text-xs text-gray-500 mt-0.5">{stage.description}</p>}
                          </div>
                          <div className="flex items-center gap-1">
                            <button onClick={() => openEditStage(stage)} className="p-1.5 text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded">
                              <i className="ri-pencil-line text-sm" />
                            </button>
                            <button onClick={() => handleRemoveStage(stage.id)} className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded">
                              <i className="ri-delete-bin-line text-sm" />
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </>
          )}
        </>
      )}

      {/* Stage form modal */}
      {showStageForm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
              <h2 className="text-lg font-semibold text-gray-800">{editStage ? 'Edit Stage' : 'Add Stage'}</h2>
              <button onClick={() => setShowStageForm(false)} className="text-gray-400 hover:text-gray-600">
                <i className="ri-close-line text-xl" />
              </button>
            </div>
            <div className="px-6 py-5 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Stage Name <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={stageForm.stageName}
                  onChange={(e) => setStageForm((f) => ({ ...f, stageName: e.target.value }))}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30"
                />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Order</label>
                  <input type="number" value={stageForm.stageOrder} onChange={(e) => setStageForm((f) => ({ ...f, stageOrder: parseInt(e.target.value) || 1 }))}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30" min={1} />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Default Role</label>
                  <input type="text" value={stageForm.defaultOwnerRole} onChange={(e) => setStageForm((f) => ({ ...f, defaultOwnerRole: e.target.value }))}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30" placeholder="e.g. Paralegal" />
                </div>
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                <textarea value={stageForm.description} onChange={(e) => setStageForm((f) => ({ ...f, description: e.target.value }))}
                  rows={2} className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500/30 resize-none" />
              </div>
              <div className="flex justify-end gap-3 pt-2">
                <button onClick={() => setShowStageForm(false)} className="px-4 py-2 text-sm text-gray-600 border border-gray-300 rounded-lg hover:bg-gray-50">Cancel</button>
                <button onClick={handleSaveStage} disabled={stageSaving || !stageForm.stageName.trim()}
                  className="px-4 py-2 text-sm bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-60 flex items-center gap-2">
                  {stageSaving && <i className="ri-loader-4-line animate-spin" />}
                  {editStage ? 'Save' : 'Add Stage'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
