'use client';

import { useState, useTransition, useRef } from 'react';
import { updateCaseStatus, addCaseNote, addPublicReply } from '@/app/support/actions';
import type { SupportCaseDetail, SupportCaseStatus, SupportNote } from '@/types/control-center';
import { ProductRefList } from '@/components/support/product-ref-list';

interface SupportDetailPanelProps {
  initialCase:     SupportCaseDetail;
  initialComments: SupportNote[];
}

const ALL_STATUSES: SupportCaseStatus[] = ['Open', 'Investigating', 'Resolved', 'Closed'];

const STATUS_STYLES: Record<SupportCaseStatus, string> = {
  Open:          'bg-blue-100   text-blue-700   border-blue-300',
  Investigating: 'bg-amber-100  text-amber-700  border-amber-300',
  Resolved:      'bg-green-100  text-green-700  border-green-300',
  Closed:        'bg-gray-100   text-gray-500   border-gray-300',
};

/**
 * SupportDetailPanel — interactive case detail view.
 *
 * Client component — handles status changes, internal note submission, and
 * customer-visible reply submission with optimistic updates.
 *
 * Access: rendered only inside PlatformAdmin-gated SupportCaseDetailPage.
 */
export function SupportDetailPanel({ initialCase, initialComments }: SupportDetailPanelProps) {
  const [kase, setKase]               = useState<SupportCaseDetail>(initialCase);
  const [comments, setComments]       = useState<SupportNote[]>(initialComments);
  const [isPending, startTransition]  = useTransition();

  // ── Status ────────────────────────────────────────────────────────────────
  const [statusError, setStatusError]   = useState<string | null>(null);
  const [savingStatus, setSavingStatus] = useState(false);

  // ── Internal note ─────────────────────────────────────────────────────────
  const [noteText, setNoteText]         = useState('');
  const [noteError, setNoteError]       = useState<string | null>(null);
  const [noteSuccess, setNoteSuccess]   = useState(false);
  const [savingNote, setSavingNote]     = useState(false);
  const noteTextareaRef                 = useRef<HTMLTextAreaElement>(null);

  // ── Public reply ──────────────────────────────────────────────────────────
  const [replyText, setReplyText]       = useState('');
  const [replyError, setReplyError]     = useState<string | null>(null);
  const [replySuccess, setReplySuccess] = useState(false);
  const [savingReply, setSavingReply]   = useState(false);
  const replyTextareaRef                = useRef<HTMLTextAreaElement>(null);

  // ── Helpers ───────────────────────────────────────────────────────────────

  function handleStatusChange(newStatus: SupportCaseStatus) {
    if (newStatus === kase.status || isPending || savingStatus) return;

    const prev = kase.status;
    setStatusError(null);
    setSavingStatus(true);
    setKase(k => ({ ...k, status: newStatus }));

    startTransition(async () => {
      const result = await updateCaseStatus(kase.id, newStatus);
      if (!result.success) {
        setKase(k => ({ ...k, status: prev }));
        setStatusError(result.error ?? 'Failed to update status.');
      }
      setSavingStatus(false);
    });
  }

  function handleAddNote() {
    if (!noteText.trim() || savingNote) return;

    const optimisticNote: SupportNote = {
      id:           `optimistic-${Date.now()}`,
      caseId:       kase.id,
      message:      noteText.trim(),
      createdBy:    'Platform Admin',
      createdAtUtc: new Date().toISOString(),
      visibility:   'Internal',
      commentType:  'Internal',
    };

    setNoteError(null);
    setNoteSuccess(false);
    setSavingNote(true);
    setComments(cs => [...cs, optimisticNote]);
    setNoteText('');

    startTransition(async () => {
      const result = await addCaseNote(kase.id, optimisticNote.message);
      if (result.success && result.note) {
        setComments(cs => cs.map(c => c.id === optimisticNote.id ? result.note! : c));
        setNoteSuccess(true);
        setTimeout(() => setNoteSuccess(false), 3000);
      } else {
        setComments(cs => cs.filter(c => c.id !== optimisticNote.id));
        setNoteText(optimisticNote.message);
        setNoteError(result.error ?? 'Failed to add note.');
      }
      setSavingNote(false);
    });
  }

  function handleAddReply() {
    if (!replyText.trim() || savingReply) return;

    const optimisticReply: SupportNote = {
      id:           `optimistic-reply-${Date.now()}`,
      caseId:       kase.id,
      message:      replyText.trim(),
      createdBy:    'Platform Admin',
      createdAtUtc: new Date().toISOString(),
      visibility:   'CustomerVisible',
      commentType:  'Normal',
    };

    setReplyError(null);
    setReplySuccess(false);
    setSavingReply(true);
    setComments(cs => [...cs, optimisticReply]);
    setReplyText('');

    startTransition(async () => {
      const result = await addPublicReply(kase.id, optimisticReply.message);
      if (result.success && result.note) {
        setComments(cs => cs.map(c => c.id === optimisticReply.id ? result.note! : c));
        setReplySuccess(true);
        setTimeout(() => setReplySuccess(false), 3000);
      } else {
        setComments(cs => cs.filter(c => c.id !== optimisticReply.id));
        setReplyText(optimisticReply.message);
        setReplyError(result.error ?? 'Failed to send reply.');
      }
      setSavingReply(false);
    });
  }

  const sortedComments   = [...comments].sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
  const publicComments   = sortedComments.filter(c => c.visibility === 'CustomerVisible');
  const internalComments = sortedComments.filter(c => c.visibility !== 'CustomerVisible');

  return (
    <div className="space-y-5">

      {/* ── Case metadata card ── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Case Details
          </h2>
          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border ${STATUS_STYLES[kase.status]}`}>
            {kase.status}
          </span>
        </div>
        <dl className="divide-y divide-gray-100">
          <MetaRow label="Tenant"   value={kase.tenantName} />
          <MetaRow label="Category" value={kase.category} />
          <MetaRow label="Priority" value={kase.priority} />
          {kase.userName && <MetaRow label="User" value={kase.userName} />}
          <MetaRow label="Opened"  value={formatDate(kase.createdAtUtc)} />
          <MetaRow label="Updated" value={formatDate(kase.updatedAtUtc)} />
        </dl>
      </div>

      {/* ── Product References ── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Product References
          </h2>
          <span className="text-xs text-gray-400 tabular-nums">
            {kase.productRefs.length} linked
          </span>
        </div>
        <ProductRefList refs={kase.productRefs} />
      </div>

      {/* ── Status controls ── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Update Status
          </h2>
        </div>
        <div className="p-5">
          <div className="flex flex-wrap gap-2">
            {ALL_STATUSES.map(s => (
              <button
                key={s}
                onClick={() => handleStatusChange(s)}
                disabled={s === kase.status || savingStatus}
                className={[
                  'px-4 py-1.5 text-sm font-medium rounded-md border transition-colors',
                  'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
                  'disabled:cursor-not-allowed',
                  s === kase.status
                    ? `${STATUS_STYLES[s]} opacity-100 cursor-default`
                    : 'border-gray-300 bg-white text-gray-700 hover:bg-gray-50 disabled:opacity-50',
                ].join(' ')}
              >
                {s}
              </button>
            ))}
          </div>
          {savingStatus && (
            <p className="text-xs text-gray-400 mt-2 flex items-center gap-1.5">
              <span className="h-3 w-3 rounded-full border-2 border-gray-400 border-t-transparent animate-spin" />
              Updating…
            </p>
          )}
          {statusError && (
            <p className="text-xs text-red-600 mt-2 font-medium">{statusError}</p>
          )}
        </div>
      </div>

      {/* ── Conversation (customer-visible) ── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Customer Conversation
          </h2>
          <span className="text-xs text-gray-400 tabular-nums">
            {publicComments.length} message{publicComments.length !== 1 ? 's' : ''}
          </span>
        </div>

        {publicComments.length === 0 ? (
          <p className="px-5 py-4 text-sm text-gray-400">No messages yet. Send a reply to start the conversation.</p>
        ) : (
          <div className="divide-y divide-gray-100">
            {publicComments.map(comment => (
              <ConversationRow key={comment.id} note={comment} isAdmin />
            ))}
          </div>
        )}

        {/* Reply to customer form */}
        <div className="border-t border-gray-100 p-5">
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
            Reply to Customer
          </label>
          <textarea
            ref={replyTextareaRef}
            value={replyText}
            onChange={e => setReplyText(e.target.value)}
            placeholder="Write a reply visible to the tenant user…"
            rows={3}
            disabled={savingReply}
            className={[
              'w-full text-sm border rounded-md px-3 py-2 resize-none',
              'focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent',
              'disabled:bg-gray-50 disabled:text-gray-400',
              replyError ? 'border-red-300' : 'border-gray-300',
            ].join(' ')}
          />
          <div className="flex items-center justify-between mt-2">
            <div>
              {replyError && (
                <p className="text-xs text-red-600 font-medium">{replyError}</p>
              )}
              {replySuccess && (
                <p className="text-xs text-green-600 font-medium">Reply sent.</p>
              )}
            </div>
            <button
              onClick={handleAddReply}
              disabled={!replyText.trim() || savingReply}
              className={[
                'inline-flex items-center gap-2 px-4 py-1.5 text-sm font-medium rounded-md transition-colors',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
                'disabled:opacity-40 disabled:cursor-not-allowed',
                replyText.trim() && !savingReply
                  ? 'bg-indigo-600 text-white hover:bg-indigo-700'
                  : 'bg-gray-100 text-gray-500',
              ].join(' ')}
            >
              {savingReply ? (
                <>
                  <span className="h-3.5 w-3.5 rounded-full border-2 border-white/30 border-t-white animate-spin" />
                  Sending…
                </>
              ) : 'Send Reply'}
            </button>
          </div>
        </div>
      </div>

      {/* ── Internal Notes ── */}
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3.5 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            Internal Notes
          </h2>
          <span className="text-xs text-gray-400 tabular-nums">
            {internalComments.length} note{internalComments.length !== 1 ? 's' : ''}
          </span>
        </div>

        {internalComments.length > 0 ? (
          <div className="divide-y divide-gray-100">
            {internalComments.map(note => (
              <NoteRow key={note.id} note={note} />
            ))}
          </div>
        ) : (
          <p className="px-5 py-4 text-sm text-gray-400">No internal notes yet.</p>
        )}

        {/* Add note form */}
        <div className="border-t border-gray-100 p-5">
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
            Add Internal Note
          </label>
          <textarea
            ref={noteTextareaRef}
            value={noteText}
            onChange={e => setNoteText(e.target.value)}
            placeholder="Add an internal note visible only to platform admins…"
            rows={3}
            disabled={savingNote}
            className={[
              'w-full text-sm border rounded-md px-3 py-2 resize-none',
              'focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent',
              'disabled:bg-gray-50 disabled:text-gray-400',
              noteError ? 'border-red-300' : 'border-gray-300',
            ].join(' ')}
          />
          <div className="flex items-center justify-between mt-2">
            <div>
              {noteError && (
                <p className="text-xs text-red-600 font-medium">{noteError}</p>
              )}
              {noteSuccess && (
                <p className="text-xs text-green-600 font-medium">Note added.</p>
              )}
            </div>
            <button
              onClick={handleAddNote}
              disabled={!noteText.trim() || savingNote}
              className={[
                'px-4 py-1.5 text-sm font-medium rounded-md transition-colors',
                'focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 focus-visible:ring-offset-1',
                'disabled:opacity-40 disabled:cursor-not-allowed',
                noteText.trim() && !savingNote
                  ? 'bg-gray-700 text-white hover:bg-gray-800'
                  : 'bg-gray-100 text-gray-500',
              ].join(' ')}
            >
              {savingNote ? (
                <span className="flex items-center gap-1.5">
                  <span className="h-3.5 w-3.5 rounded-full border-2 border-gray-400 border-t-transparent animate-spin" />
                  Adding…
                </span>
              ) : 'Add Note'}
            </button>
          </div>
        </div>
      </div>

    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────────────────

function MetaRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline gap-4 px-5 py-2.5">
      <dt className="text-xs text-gray-400 font-medium w-20 shrink-0">{label}</dt>
      <dd className="text-sm text-gray-700">{value}</dd>
    </div>
  );
}

function ConversationRow({ note, isAdmin }: { note: SupportNote; isAdmin?: boolean }) {
  const isOptimistic = note.id.startsWith('optimistic-');
  const align = isAdmin ? 'items-end' : 'items-start';
  const bubbleBg = isAdmin
    ? 'bg-indigo-600 text-white'
    : 'bg-gray-100 text-gray-800';
  const metaAlign = isAdmin ? 'justify-end' : 'justify-start';

  return (
    <div className={`px-5 py-4 flex flex-col ${align} gap-1 ${isOptimistic ? 'opacity-60' : ''}`}>
      <div className={`flex items-center gap-2 text-xs text-gray-400 ${metaAlign}`}>
        <span className="font-semibold text-gray-600">{note.createdBy}</span>
        <span className="text-gray-300">·</span>
        <span>{formatDate(note.createdAtUtc)}</span>
        {isOptimistic && <span className="italic text-[10px]">sending…</span>}
      </div>
      <div className={`max-w-prose px-3.5 py-2.5 rounded-xl text-sm leading-relaxed ${bubbleBg}`}>
        {note.message}
      </div>
    </div>
  );
}

function NoteRow({ note }: { note: SupportNote }) {
  const isOptimistic = note.id.startsWith('optimistic-');
  return (
    <div className={`px-5 py-4 ${isOptimistic ? 'opacity-60' : ''}`}>
      <div className="flex items-center gap-2 mb-1.5">
        <span className="text-xs font-semibold text-gray-700">{note.createdBy}</span>
        <span className="text-gray-300">·</span>
        <span className="text-xs text-gray-400">{formatDate(note.createdAtUtc)}</span>
        {isOptimistic && (
          <span className="text-[10px] text-gray-400 italic">saving…</span>
        )}
      </div>
      <p className="text-sm text-gray-700 leading-relaxed">{note.message}</p>
    </div>
  );
}

// ── helpers ───────────────────────────────────────────────────────────────────

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month:   'short',
      day:     'numeric',
      year:    'numeric',
      hour:    '2-digit',
      minute:  '2-digit',
      hour12:  false,
      timeZone: 'UTC',
    }) + ' UTC';
  } catch {
    return iso;
  }
}
