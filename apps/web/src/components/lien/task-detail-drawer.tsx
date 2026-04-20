'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { lienTaskNotesService } from '@/lib/liens/lien-task-notes.service';
import type { TaskNoteResponse } from '@/lib/liens/lien-task-notes.types';
import type { TaskDto, TaskStatus } from '@/lib/liens/lien-tasks.types';
import { TASK_STATUS_LABELS, TASK_STATUS_COLORS, TASK_PRIORITY_COLORS, TASK_PRIORITY_ICONS, ALL_TASK_STATUSES } from '@/lib/liens/lien-tasks.types';
import { lienTasksService } from '@/lib/liens/lien-tasks.service';
import { formatDateTime } from '@/lib/lien-utils';
import { getNoteInitials } from '@/lib/liens/note-utils';

interface TaskDetailDrawerProps {
  task: TaskDto | null;
  onClose: () => void;
  onEdit: (task: TaskDto) => void;
  onStatusChange?: (updated: TaskDto) => void;
}

const MAX_CHARS = 5000;

function formatDate(val?: string | null): string {
  if (!val) return '\u2014';
  try {
    const d = new Date(val);
    return isNaN(d.getTime()) ? val : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  } catch { return val ?? '\u2014'; }
}

export function TaskDetailDrawer({ task, onClose, onEdit, onStatusChange }: TaskDetailDrawerProps) {
  const [activeTab, setActiveTab] = useState<'notes' | 'details'>('notes');
  const [notes, setNotes] = useState<TaskNoteResponse[]>([]);
  const [loadingNotes, setLoadingNotes] = useState(false);
  const [noteError, setNoteError] = useState<string | null>(null);

  const [draftText, setDraftText] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const [editingNoteId, setEditingNoteId] = useState<string | null>(null);
  const [editingText, setEditingText] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const [statusChanging, setStatusChanging] = useState<TaskStatus | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const [localTask, setLocalTask] = useState<TaskDto | null>(task);

  const threadEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setLocalTask(task);
    setStatusError(null);
    setStatusChanging(null);
  }, [task]);

  const fetchNotes = useCallback(async () => {
    if (!task) return;
    setLoadingNotes(true);
    setNoteError(null);
    try {
      const data = await lienTaskNotesService.getNotes(task.id);
      setNotes(data);
    } catch {
      setNoteError('Failed to load notes.');
    } finally {
      setLoadingNotes(false);
    }
  }, [task]);

  useEffect(() => {
    if (task) {
      setActiveTab('notes');
      setDraftText('');
      setEditingNoteId(null);
      fetchNotes();
    }
  }, [task, fetchNotes]);

  async function handleStatusChange(newStatus: TaskStatus) {
    if (!localTask || newStatus === localTask.status) return;
    setStatusChanging(newStatus);
    setStatusError(null);
    try {
      const updated = await lienTasksService.updateStatus(localTask.id, newStatus);
      setLocalTask(updated);
      onStatusChange?.(updated);
    } catch {
      setStatusError('Failed to update status. Please try again.');
    } finally {
      setStatusChanging(null);
    }
  }

  useEffect(() => {
    if (activeTab === 'notes') {
      threadEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [notes, activeTab]);

  async function handleAddNote() {
    if (!task || !draftText.trim()) return;
    setSubmitting(true);
    try {
      const note = await lienTaskNotesService.createNote(task.id, draftText.trim());
      setNotes((prev) => [...prev, note]);
      setDraftText('');
    } catch {
      setNoteError('Failed to add note.');
    } finally {
      setSubmitting(false);
    }
  }

  function startEdit(note: TaskNoteResponse) {
    setEditingNoteId(note.id);
    setEditingText(note.content);
  }

  async function handleSaveEdit(noteId: string) {
    if (!task || !editingText.trim()) return;
    setSavingEdit(true);
    try {
      const updated = await lienTaskNotesService.updateNote(task.id, noteId, editingText.trim());
      setNotes((prev) => prev.map((n) => (n.id === noteId ? updated : n)));
      setEditingNoteId(null);
    } catch {
      setNoteError('Failed to update note.');
    } finally {
      setSavingEdit(false);
    }
  }

  async function handleDelete(noteId: string) {
    if (!task) return;
    setDeletingId(noteId);
    try {
      await lienTaskNotesService.deleteNote(task.id, noteId);
      setNotes((prev) => prev.filter((n) => n.id !== noteId));
    } catch {
      setNoteError('Failed to delete note.');
    } finally {
      setDeletingId(null);
    }
  }

  if (!localTask) return null;

  const displayTask = localTask;
  const statusCfg = TASK_STATUS_COLORS[displayTask.status];
  const isSystem = displayTask.isSystemGenerated;
  const isTerminal = displayTask.status === 'COMPLETED' || displayTask.status === 'CANCELLED';

  const STATUS_ICONS: Record<string, string> = {
    NEW: 'ri-circle-line',
    IN_PROGRESS: 'ri-play-circle-line',
    WAITING_BLOCKED: 'ri-pause-circle-line',
    COMPLETED: 'ri-checkbox-circle-line',
    CANCELLED: 'ri-close-circle-line',
  };

  return (
    <>
      <div
        className="fixed inset-0 bg-black/30 z-40 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        className="fixed right-0 top-0 h-full w-full max-w-xl bg-white shadow-2xl z-50 flex flex-col"
        role="dialog"
        aria-label="Task details"
      >
        {/* Header */}
        <div className="flex items-start justify-between px-6 py-4 border-b border-gray-100 flex-shrink-0">
          <div className="flex-1 min-w-0 pr-4">
            <div className="flex items-center gap-2 mb-1 flex-wrap">
              {isSystem && (
                <span className="inline-flex items-center gap-1 text-xs font-medium bg-violet-100 text-violet-700 px-2 py-0.5 rounded-full">
                  <i className="ri-robot-line" />
                  System Generated
                </span>
              )}
              <span className={`inline-flex text-xs font-medium px-2 py-0.5 rounded-full ${statusCfg.bg} ${statusCfg.text}`}>
                {TASK_STATUS_LABELS[displayTask.status]}
              </span>
            </div>
            <h2 className="text-base font-semibold text-gray-900 leading-snug line-clamp-2">
              {displayTask.title}
            </h2>
          </div>
          <div className="flex items-center gap-1 flex-shrink-0">
            {!isTerminal && (
              <button
                onClick={() => onEdit(displayTask)}
                className="p-2 text-gray-500 hover:text-primary hover:bg-primary/5 rounded-lg transition-colors"
                title="Edit task"
              >
                <i className="ri-edit-line" />
              </button>
            )}
            <button
              onClick={onClose}
              className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg"
              title="Close"
            >
              <i className="ri-close-line" />
            </button>
          </div>
        </div>

        {/* Status change bar */}
        <div className="px-6 py-3 border-b border-gray-100 flex-shrink-0">
          <p className="text-xs text-gray-400 mb-2 font-medium uppercase tracking-wide">Change status</p>
          <div className="flex flex-wrap gap-2">
            {ALL_TASK_STATUSES.map((s) => {
              const cfg = TASK_STATUS_COLORS[s];
              const isCurrent = s === displayTask.status;
              const isLoading = statusChanging === s;
              return (
                <button
                  key={s}
                  onClick={() => handleStatusChange(s)}
                  disabled={isCurrent || statusChanging !== null}
                  title={isCurrent ? 'Current status' : `Set to ${TASK_STATUS_LABELS[s]}`}
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium transition-all border
                    ${isCurrent
                      ? `${cfg.bg} ${cfg.text} border-current ring-2 ring-offset-1 ring-current/30 cursor-default`
                      : `bg-white text-gray-500 border-gray-200 hover:${cfg.bg} hover:${cfg.text} hover:border-current disabled:opacity-40 disabled:cursor-not-allowed`
                    }`}
                >
                  {isLoading
                    ? <i className="ri-loader-4-line animate-spin" />
                    : <i className={STATUS_ICONS[s]} />
                  }
                  {TASK_STATUS_LABELS[s]}
                </button>
              );
            })}
          </div>
          {statusError && (
            <p className="mt-2 text-xs text-red-600 flex items-center gap-1">
              <i className="ri-error-warning-line" />
              {statusError}
            </p>
          )}
        </div>

        {/* Task meta */}
        <div className="px-6 py-3 border-b border-gray-100 flex-shrink-0">
          <div className="grid grid-cols-2 gap-3 text-xs text-gray-500">
            <div className="flex items-center gap-1.5">
              <i className={`${TASK_PRIORITY_ICONS[displayTask.priority]} ${TASK_PRIORITY_COLORS[displayTask.priority]}`} />
              <span className="font-medium text-gray-700">
                {displayTask.priority.charAt(0) + displayTask.priority.slice(1).toLowerCase()} priority
              </span>
            </div>
            {displayTask.dueDate && (
              <div className="flex items-center gap-1.5">
                <i className="ri-calendar-line text-gray-400" />
                <span>Due {formatDate(displayTask.dueDate)}</span>
              </div>
            )}
            {displayTask.linkedLiens.length > 0 && (
              <div className="flex items-center gap-1.5">
                <i className="ri-links-line text-purple-400" />
                <span>{displayTask.linkedLiens.length} lien{displayTask.linkedLiens.length !== 1 ? 's' : ''} linked</span>
              </div>
            )}
          </div>
          {displayTask.description && (
            <p className="mt-2 text-sm text-gray-600 leading-relaxed">{displayTask.description}</p>
          )}
        </div>

        {/* Tabs */}
        <div className="flex border-b border-gray-100 flex-shrink-0">
          {(['notes', 'details'] as const).map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-6 py-2.5 text-sm font-medium transition-colors border-b-2 -mb-px ${
                activeTab === tab
                  ? 'border-primary text-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              {tab === 'notes' ? (
                <span className="flex items-center gap-1.5">
                  <i className="ri-chat-3-line" />
                  Notes
                  {notes.length > 0 && (
                    <span className="ml-1 bg-primary/10 text-primary text-xs rounded-full px-1.5 py-0.5 font-semibold">
                      {notes.length}
                    </span>
                  )}
                </span>
              ) : (
                <span className="flex items-center gap-1.5">
                  <i className="ri-information-line" />
                  {isSystem ? 'Automation Details' : 'Details'}
                </span>
              )}
            </button>
          ))}
        </div>

        {/* Tab content */}
        <div className="flex-1 overflow-y-auto">
          {activeTab === 'notes' ? (
            <div className="flex flex-col h-full">
              {/* Thread */}
              <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
                {loadingNotes && (
                  <div className="flex justify-center py-8">
                    <i className="ri-loader-4-line animate-spin text-xl text-gray-300" />
                  </div>
                )}
                {noteError && (
                  <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-2 flex items-center justify-between">
                    <span className="text-sm text-red-600">{noteError}</span>
                    <button
                      onClick={() => { setNoteError(null); fetchNotes(); }}
                      className="text-xs text-red-500 hover:text-red-700 font-medium ml-2"
                    >
                      Retry
                    </button>
                  </div>
                )}
                {!loadingNotes && notes.length === 0 && !noteError && (
                  <div className="flex flex-col items-center justify-center py-12 text-gray-300">
                    <i className="ri-chat-3-line text-4xl mb-2" />
                    <p className="text-sm">No notes yet. Add the first one below.</p>
                  </div>
                )}
                {notes.map((note) => (
                  <div key={note.id} className="flex gap-3">
                    <div className="w-8 h-8 rounded-full bg-primary/10 text-primary flex items-center justify-center text-xs font-bold flex-shrink-0 mt-0.5">
                      {getNoteInitials(note.createdByName)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="text-xs font-medium text-gray-700">
                          {note.createdByName || 'User'}
                        </span>
                        {note.isEdited && (
                          <span className="text-xs text-gray-400 italic">edited</span>
                        )}
                        <span className="text-xs text-gray-400 ml-auto">
                          {formatDateTime(note.createdAtUtc)}
                        </span>
                      </div>

                      {editingNoteId === note.id ? (
                        <div>
                          <textarea
                            value={editingText}
                            onChange={(e) => setEditingText(e.target.value)}
                            rows={3}
                            maxLength={MAX_CHARS}
                            className="w-full border border-primary/40 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 resize-none"
                          />
                          <div className="flex items-center justify-between mt-1.5">
                            <span className="text-xs text-gray-400">
                              {editingText.length}/{MAX_CHARS}
                            </span>
                            <div className="flex gap-2">
                              <button
                                onClick={() => setEditingNoteId(null)}
                                className="text-xs text-gray-500 hover:text-gray-700 px-2 py-1"
                              >
                                Cancel
                              </button>
                              <button
                                onClick={() => handleSaveEdit(note.id)}
                                disabled={savingEdit || !editingText.trim()}
                                className="text-xs bg-primary text-white px-3 py-1 rounded-lg hover:bg-primary/90 disabled:opacity-50"
                              >
                                {savingEdit ? 'Saving…' : 'Save'}
                              </button>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="bg-gray-50 rounded-xl px-4 py-3 text-sm text-gray-700 whitespace-pre-wrap leading-relaxed relative group">
                          {note.content}
                          <div className="absolute top-2 right-2 hidden group-hover:flex items-center gap-1">
                            <button
                              onClick={() => startEdit(note)}
                              className="p-1 text-gray-400 hover:text-primary rounded"
                              title="Edit note"
                            >
                              <i className="ri-edit-line text-xs" />
                            </button>
                            <button
                              onClick={() => handleDelete(note.id)}
                              disabled={deletingId === note.id}
                              className="p-1 text-gray-400 hover:text-red-500 rounded"
                              title="Delete note"
                            >
                              {deletingId === note.id
                                ? <i className="ri-loader-4-line animate-spin text-xs" />
                                : <i className="ri-delete-bin-line text-xs" />}
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                ))}
                <div ref={threadEndRef} />
              </div>

              {/* Compose */}
              <div className="px-6 py-4 border-t border-gray-100 flex-shrink-0 bg-white">
                <textarea
                  value={draftText}
                  onChange={(e) => setDraftText(e.target.value)}
                  placeholder="Write a note..."
                  rows={3}
                  maxLength={MAX_CHARS}
                  className="w-full border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
                />
                <div className="flex items-center justify-between mt-2">
                  <span className="text-xs text-gray-400">{draftText.length}/{MAX_CHARS}</span>
                  <button
                    onClick={handleAddNote}
                    disabled={submitting || !draftText.trim()}
                    className="flex items-center gap-1.5 text-sm font-medium px-4 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed transition-opacity"
                  >
                    {submitting ? <i className="ri-loader-4-line animate-spin" /> : <i className="ri-send-plane-line" />}
                    {submitting ? 'Posting…' : 'Post'}
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="px-6 py-5 space-y-5">
              {isSystem ? (
                <AutomationDetailsPanel task={displayTask} />
              ) : (
                <ManualTaskDetails task={displayTask} />
              )}
            </div>
          )}
        </div>
      </aside>
    </>
  );
}

function AutomationDetailsPanel({ task }: { task: TaskDto }) {
  return (
    <div className="space-y-4">
      <div className="bg-violet-50 border border-violet-200 rounded-xl p-4">
        <div className="flex items-center gap-2 mb-3">
          <i className="ri-robot-line text-violet-600 text-lg" />
          <h3 className="text-sm font-semibold text-violet-900">Automation Details</h3>
        </div>
        <p className="text-xs text-violet-700 leading-relaxed">
          This task was generated automatically by the LegalSynq rules engine based on
          a configured trigger and template.
        </p>
      </div>

      <DetailRow
        icon="ri-flashlight-line"
        label="Source"
        value="System Automation"
        valueClass="text-violet-700 font-medium"
      />

      {task.generationRuleId && (
        <DetailRow
          icon="ri-settings-3-line"
          label="Generation Rule ID"
          value={task.generationRuleId}
          mono
        />
      )}

      {task.generatingTemplateId && (
        <DetailRow
          icon="ri-file-copy-2-line"
          label="Task Template ID"
          value={task.generatingTemplateId}
          mono
        />
      )}

      {task.caseId && (
        <DetailRow
          icon="ri-folder-line"
          label="Triggered for Case"
          value={task.caseId}
          mono
        />
      )}

      <div className="rounded-xl border border-gray-100 bg-gray-50 p-4 space-y-2">
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
          How this works
        </p>
        <div className="flex gap-2 text-xs text-gray-600">
          <span className="flex-shrink-0 w-5 h-5 rounded-full bg-violet-100 text-violet-600 font-bold flex items-center justify-center text-[10px]">1</span>
          <span>A domain event (e.g. lien status change, case stage update) was emitted by the system.</span>
        </div>
        <div className="flex gap-2 text-xs text-gray-600">
          <span className="flex-shrink-0 w-5 h-5 rounded-full bg-violet-100 text-violet-600 font-bold flex items-center justify-center text-[10px]">2</span>
          <span>The matching generation rule evaluated the event and found a configured template.</span>
        </div>
        <div className="flex gap-2 text-xs text-gray-600">
          <span className="flex-shrink-0 w-5 h-5 rounded-full bg-violet-100 text-violet-600 font-bold flex items-center justify-center text-[10px]">3</span>
          <span>This task was created from the template, pre-filled with the relevant entity data.</span>
        </div>
      </div>

      <ManualTaskDetails task={task} />
    </div>
  );
}

function ManualTaskDetails({ task }: { task: TaskDto }) {
  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Task Info</p>
      <DetailRow icon="ri-hashtag" label="Task ID" value={task.id} mono />
      <DetailRow icon="ri-user-add-line" label="Created by" value={task.createdByUserId ?? '\u2014'} mono={!!task.createdByUserId} />
      <DetailRow icon="ri-calendar-check-line" label="Created" value={formatDateTime(task.createdAtUtc)} />
      <DetailRow icon="ri-refresh-line" label="Last updated" value={formatDateTime(task.updatedAtUtc)} />
      {task.completedAt && (
        <DetailRow icon="ri-checkbox-circle-line" label="Completed" value={formatDateTime(task.completedAt)} />
      )}
      {task.workflowStageId && (
        <DetailRow icon="ri-flow-chart" label="Workflow Stage" value={task.workflowStageId} mono />
      )}
      {task.workflowInstanceId && (
        <LinkedWorkflowSection instanceId={task.workflowInstanceId} stepKey={task.workflowStepKey} />
      )}
    </div>
  );
}

function LinkedWorkflowSection({ instanceId, stepKey }: { instanceId: string; stepKey?: string }) {
  return (
    <div className="mt-2 rounded-xl border border-blue-100 bg-blue-50 p-3 space-y-2">
      <div className="flex items-center gap-2 mb-1">
        <i className="ri-links-line text-blue-600" />
        <p className="text-xs font-semibold text-blue-800">Linked Workflow Instance</p>
      </div>
      <div className="space-y-1 text-xs">
        <div>
          <p className="text-blue-600 mb-0.5">Instance ID</p>
          <p className="font-mono text-blue-900 break-all">{instanceId}</p>
        </div>
        {stepKey && (
          <div>
            <p className="text-blue-600 mb-0.5">Flow Step</p>
            <p className="font-mono text-blue-900">{stepKey}</p>
          </div>
        )}
      </div>
    </div>
  );
}

function DetailRow({
  icon,
  label,
  value,
  mono = false,
  valueClass = '',
}: {
  icon: string;
  label: string;
  value: string;
  mono?: boolean;
  valueClass?: string;
}) {
  return (
    <div className="flex items-start gap-3 py-2 border-b border-gray-50 last:border-0">
      <i className={`${icon} text-gray-400 mt-0.5 flex-shrink-0`} />
      <div className="flex-1 min-w-0">
        <p className="text-xs text-gray-400 mb-0.5">{label}</p>
        <p className={`text-sm text-gray-700 break-all ${mono ? 'font-mono text-xs' : ''} ${valueClass}`}>
          {value}
        </p>
      </div>
    </div>
  );
}
