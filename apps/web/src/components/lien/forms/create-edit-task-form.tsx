'use client';

import { useState } from 'react';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import type { TaskDto, CreateTaskRequest, UpdateTaskRequest, TaskPriority } from '@/lib/liens/lien-tasks.types';
import { ALL_TASK_STATUSES, TASK_STATUS_LABELS } from '@/lib/liens/lien-tasks.types';

interface CreateEditTaskFormProps {
  open: boolean;
  onClose: () => void;
  onSaved: (task: TaskDto) => void;
  prefillCaseId?: string;
  prefillLienId?: string;
  editTask?: TaskDto;
}

const PRIORITIES: { value: TaskPriority; label: string; icon: string; color: string }[] = [
  { value: 'LOW',    label: 'Low',    icon: 'ri-arrow-down-line',     color: 'text-gray-500' },
  { value: 'MEDIUM', label: 'Medium', icon: 'ri-subtract-line',       color: 'text-blue-500' },
  { value: 'HIGH',   label: 'High',   icon: 'ri-arrow-up-line',       color: 'text-orange-500' },
  { value: 'URGENT', label: 'Urgent', icon: 'ri-alarm-warning-line',  color: 'text-red-600' },
];

export function CreateEditTaskForm({
  open,
  onClose,
  onSaved,
  prefillCaseId,
  prefillLienId,
  editTask,
}: CreateEditTaskFormProps) {
  const isEdit = !!editTask;

  const [title, setTitle] = useState(editTask?.title ?? '');
  const [description, setDescription] = useState(editTask?.description ?? '');
  const [priority, setPriority] = useState<TaskPriority>(editTask?.priority ?? 'MEDIUM');
  const [dueDate, setDueDate] = useState(editTask?.dueDate ? editTask.dueDate.split('T')[0] : '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
          <h2 className="text-lg font-semibold text-gray-800">
            {isEdit ? 'Edit Task' : 'Create Task'}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <i className="ri-close-line text-xl" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-5 space-y-4">
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm text-red-700">
              {error}
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
              onClick={onClose}
              className="px-4 py-2 text-sm text-gray-600 hover:text-gray-800 border border-gray-300 rounded-lg hover:bg-gray-50"
            >
              Cancel
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
      </div>
    </div>
  );
}
