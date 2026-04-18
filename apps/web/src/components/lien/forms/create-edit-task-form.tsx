'use client';

import { useState, useEffect } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import { lienTaskTemplatesService } from '@/lib/liens/lien-task-templates.service';
import type { TaskDto, CreateTaskRequest, UpdateTaskRequest, TaskPriority } from '@/lib/liens/lien-tasks.types';
import type { TaskTemplateDto, TemplateContextType } from '@/lib/liens/lien-task-templates.types';
import { TemplatePicker } from '@/components/lien/template-picker';

interface CreateEditTaskFormProps {
  open: boolean;
  onClose: () => void;
  onSaved: (task: TaskDto) => void;
  prefillCaseId?: string;
  prefillLienId?: string;
  prefillWorkflowStageId?: string;
  editTask?: TaskDto;
}

type Step = 'pick-template' | 'fill-form';

const PRIORITIES: { value: TaskPriority; label: string; icon: string; color: string }[] = [
  { value: 'LOW',    label: 'Low',    icon: 'ri-arrow-down-line',     color: 'text-gray-500' },
  { value: 'MEDIUM', label: 'Medium', icon: 'ri-subtract-line',       color: 'text-blue-500' },
  { value: 'HIGH',   label: 'High',   icon: 'ri-arrow-up-line',       color: 'text-orange-500' },
  { value: 'URGENT', label: 'Urgent', icon: 'ri-alarm-warning-line',  color: 'text-red-600' },
];

function contextTypeFromProps(prefillCaseId?: string, prefillLienId?: string): TemplateContextType {
  if (prefillCaseId) return 'CASE';
  if (prefillLienId) return 'LIEN';
  return 'GENERAL';
}

function calcDueDate(offsetDays?: number | null): string {
  if (!offsetDays) return '';
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  return d.toISOString().split('T')[0];
}

export function CreateEditTaskForm({
  open,
  onClose,
  onSaved,
  prefillCaseId,
  prefillLienId,
  prefillWorkflowStageId,
  editTask,
}: CreateEditTaskFormProps) {
  const isEdit = !!editTask;
  const contextType = contextTypeFromProps(prefillCaseId, prefillLienId);

  const [step, setStep] = useState<Step>(isEdit ? 'fill-form' : 'pick-template');
  const [selectedTemplate, setSelectedTemplate] = useState<TaskTemplateDto | null>(null);
  const [templates, setTemplates] = useState<TaskTemplateDto[]>([]);
  const [templatesLoading, setTemplatesLoading] = useState(false);

  const [title, setTitle] = useState(editTask?.title ?? '');
  const [description, setDescription] = useState(editTask?.description ?? '');
  const [priority, setPriority] = useState<TaskPriority>(editTask?.priority ?? 'MEDIUM');
  const [dueDate, setDueDate] = useState(editTask?.dueDate ? editTask.dueDate.split('T')[0] : '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || isEdit) return;
    setStep('pick-template');
    setSelectedTemplate(null);
    setTitle('');
    setDescription('');
    setPriority('MEDIUM');
    setDueDate('');
    setError(null);
    setTemplatesLoading(true);
    lienTaskTemplatesService
      .getContextualTemplates({ contextType, workflowStageId: prefillWorkflowStageId })
      .then(setTemplates)
      .catch(() => setTemplates([]))
      .finally(() => setTemplatesLoading(false));
  }, [open, isEdit, contextType, prefillWorkflowStageId]);

  function handleSelectTemplate(template: TaskTemplateDto) {
    setSelectedTemplate(template);
    setTitle(template.defaultTitle);
    setDescription(template.defaultDescription ?? '');
    setPriority(template.defaultPriority as TaskPriority);
    setDueDate(calcDueDate(template.defaultDueOffsetDays));
    setStep('fill-form');
  }

  function handleScratch() {
    setSelectedTemplate(null);
    setTitle('');
    setDescription('');
    setPriority('MEDIUM');
    setDueDate('');
    setStep('fill-form');
  }

  function handleBackToTemplates() {
    setStep('pick-template');
    setError(null);
  }

  if (!open) return null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) { setError('Title is required.'); return; }
    setSaving(true);
    setError(null);
    try {
      let saved: TaskDto;
      if (isEdit) {
        const req: UpdateTaskRequest = {
          title: title.trim(),
          description: description.trim() || undefined,
          priority,
          caseId: editTask?.caseId,
          lienIds: editTask?.linkedLiens.map((l) => l.lienId) ?? [],
          dueDate: dueDate || undefined,
          workflowStageId: editTask?.workflowStageId,
        };
        saved = await lienTasksService.updateTask(editTask!.id, req);
      } else {
        const req: CreateTaskRequest = {
          title: title.trim(),
          description: description.trim() || undefined,
          priority,
          caseId: prefillCaseId,
          lienIds: prefillLienId ? [prefillLienId] : [],
          dueDate: dueDate || undefined,
          workflowStageId: selectedTemplate?.applicableWorkflowStageId ?? prefillWorkflowStageId,
          templateId: selectedTemplate?.id,
        };
        saved = await lienTasksService.createTask(req);
      }
      onSaved(saved);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save task.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg mx-4" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
          <div className="flex items-center gap-2">
            {step === 'fill-form' && !isEdit && (
              <button
                type="button"
                onClick={handleBackToTemplates}
                className="text-gray-400 hover:text-gray-600"
                title="Back to templates"
              >
                <i className="ri-arrow-left-line text-lg" />
              </button>
            )}
            <h2 className="text-lg font-semibold text-gray-800">
              {isEdit
                ? 'Edit Task'
                : step === 'pick-template'
                ? 'Create Task'
                : selectedTemplate
                ? `From: ${selectedTemplate.name}`
                : 'Create Task'}
            </h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <i className="ri-close-line text-xl" />
          </button>
        </div>

        <div className="px-6 py-5">
          {step === 'pick-template' && !isEdit ? (
            <TemplatePicker
              templates={templates}
              contextType={contextType}
              onSelect={handleSelectTemplate}
              onScratch={handleScratch}
              loading={templatesLoading}
            />
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-700">
                  {error}
                </div>
              )}

              {selectedTemplate && (
                <div className="bg-blue-50 border border-blue-100 rounded-lg px-3 py-2 flex items-center gap-2 text-xs text-blue-700">
                  <i className="ri-file-list-3-line shrink-0" />
                  <span>
                    Pre-filled from <strong>{selectedTemplate.name}</strong>. Edit fields before saving.
                  </span>
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Title <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  placeholder="Task title..."
                  required
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Description</label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={3}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30 resize-none"
                  placeholder="Optional description..."
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Priority</label>
                  <select
                    value={priority}
                    onChange={(e) => setPriority(e.target.value as TaskPriority)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  >
                    {PRIORITIES.map((p) => (
                      <option key={p.value} value={p.value}>{p.label}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Due Date</label>
                  <input
                    type="date"
                    value={dueDate}
                    onChange={(e) => setDueDate(e.target.value)}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/30"
                  />
                </div>
              </div>

              {(prefillCaseId || editTask?.caseId) && (
                <div className="text-xs text-gray-500 flex items-center gap-1">
                  <i className="ri-folder-open-line" />
                  Linked to case
                </div>
              )}

              {(prefillLienId || (editTask?.linkedLiens?.length ?? 0) > 0) && (
                <div className="text-xs text-gray-500 flex items-center gap-1">
                  <i className="ri-stack-line" />
                  {prefillLienId ? '1 lien linked' : `${editTask?.linkedLiens.length} lien(s) linked`}
                </div>
              )}

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={isEdit ? onClose : handleBackToTemplates}
                  className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800 border border-gray-300 rounded-lg hover:bg-gray-50"
                >
                  {isEdit ? 'Cancel' : 'Back'}
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-60 flex items-center gap-2"
                >
                  {saving && <i className="ri-loader-4-line animate-spin" />}
                  {isEdit ? 'Save Changes' : 'Create Task'}
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
