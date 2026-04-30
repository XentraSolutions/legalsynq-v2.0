'use client';

import { useState, useTransition, useRef } from 'react';
import { postComment } from './actions';

interface Comment {
  id:         string;
  senderType: string;
  senderName: string;
  message:    string;
  createdAt:  string;
}

interface Attachment {
  id:            string;
  fileName:      string;
  contentType:   string;
  fileSizeBytes: number;
}

interface ThreadData {
  referralId:  string;
  status:      string;
  clientName:  string;
  service:     string;
  providerName: string;
  referrerName: string | null;
  createdAt:   string;
  comments:    Comment[];
  attachments: Attachment[];
}

interface Props {
  token: string;
  data:  ThreadData;
}

const STATUS_MAP: Record<string, { label: string; color: string; bg: string; border: string }> = {
  New:        { label: 'Awaiting Your Response', color: '#92400e', bg: '#fffbeb', border: '#fcd34d' },
  NewOpened:  { label: 'Opened — Pending Response', color: '#1e40af', bg: '#eff6ff', border: '#93c5fd' },
  Accepted:   { label: 'Accepted',                  color: '#065f46', bg: '#ecfdf5', border: '#6ee7b7' },
  Rejected:   { label: 'Declined',                  color: '#991b1b', bg: '#fef2f2', border: '#fca5a5' },
  Cancelled:  { label: 'Cancelled',                 color: '#374151', bg: '#f9fafb', border: '#d1d5db' },
  InProgress: { label: 'In Progress',               color: '#5b21b6', bg: '#f5f3ff', border: '#c4b5fd' },
};

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit', hour12: true,
    });
  } catch { return iso; }
}

function formatBytes(b: number) {
  if (b < 1024)      return `${b} B`;
  if (b < 1048576)   return `${(b / 1024).toFixed(1)} KB`;
  return `${(b / 1048576).toFixed(1)} MB`;
}

const s: Record<string, React.CSSProperties> = {
  page:      { minHeight: '100vh', background: '#f8fafc', fontFamily: 'system-ui,-apple-system,sans-serif', color: '#111827' },
  header:    { background: '#0f172a', padding: '20px 24px', color: '#fff' },
  headerInner: { maxWidth: 680, margin: '0 auto' },
  label:     { margin: '0 0 4px', fontSize: 12, color: '#94a3b8', letterSpacing: '0.05em', textTransform: 'uppercase' as const },
  title:     { margin: 0, fontSize: 20, fontWeight: 700 },
  inner:     { maxWidth: 680, margin: '0 auto', padding: '24px 16px' },
  card:      { background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0', padding: '20px 24px', marginBottom: 20 },
  cardTitle: { margin: '0 0 14px', fontSize: 15, fontWeight: 700, color: '#0f172a' },
  grid2:     { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px 24px' },
  fieldLabel:{ margin: '0 0 2px', fontSize: 11, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
  fieldVal:  { margin: 0, fontSize: 14, color: '#0f172a', fontWeight: 500 },
  attRow:    { display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', borderRadius: 8,
               border: '1px solid #e2e8f0', background: '#f8fafc', marginBottom: 8, textDecoration: 'none' },
  btnPrimary:{ display: 'block', width: '100%', boxSizing: 'border-box' as const,
               background: '#1a56db', color: '#fff', border: 'none',
               padding: '11px 20px', borderRadius: 6, fontSize: 14, fontWeight: 700,
               cursor: 'pointer', textAlign: 'center' as const, textDecoration: 'none' },
  btnOutline:{ display: 'block', width: '100%', boxSizing: 'border-box' as const,
               background: '#fff', color: '#1a56db', border: '2px solid #1a56db',
               padding: '10px 20px', borderRadius: 6, fontSize: 14, fontWeight: 700,
               cursor: 'pointer', textAlign: 'center' as const, textDecoration: 'none' },
  btnDanger: { display: 'block', width: '100%', boxSizing: 'border-box' as const,
               background: '#fff', color: '#dc2626', border: '2px solid #fca5a5',
               padding: '10px 20px', borderRadius: 6, fontSize: 14, fontWeight: 700,
               cursor: 'pointer', textAlign: 'center' as const, textDecoration: 'none' },
  input:     { width: '100%', boxSizing: 'border-box' as const,
               padding: '9px 12px', fontSize: 14,
               border: '1px solid #d1d5db', borderRadius: 6, color: '#111827', fontFamily: 'inherit' },
  textarea:  { width: '100%', boxSizing: 'border-box' as const,
               padding: '9px 12px', fontSize: 14,
               border: '1px solid #d1d5db', borderRadius: 6, color: '#111827',
               fontFamily: 'inherit', resize: 'vertical' as const },
  upgradeBox:{ background: 'linear-gradient(135deg,#1e3a8a 0%,#1a56db 100%)', borderRadius: 10,
               padding: '20px 24px', marginBottom: 20, color: '#fff' },
};

export function ThreadClient({ token, data }: Props) {
  const [comments,      setComments]  = useState<Comment[]>(data.comments);
  const [senderName,    setSenderName] = useState('');
  const [message,       setMessage]   = useState('');
  const [formError,     setFormError] = useState('');
  const [sent,          setSent]      = useState(false);
  const [isPending,     startTransition] = useTransition();
  const bottomRef = useRef<HTMLDivElement>(null);

  const st = STATUS_MAP[data.status] ?? { label: data.status, color: '#374151', bg: '#f9fafb', border: '#d1d5db' };

  const isActionable = data.status === 'New' || data.status === 'NewOpened';
  const referralId   = data.referralId;
  const loginReturnTo = encodeURIComponent(`/provider/referrals/${referralId}`);
  const activateUrl  = `/referrals/activate?referralId=${referralId}&token=${encodeURIComponent(token)}`;
  const loginUrl     = `/login?returnTo=${loginReturnTo}&reason=referral-view`;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFormError('');
    setSent(false);
    startTransition(async () => {
      const result = await postComment(token, 'provider', senderName, message);
      if (!result.success) { setFormError(result.error ?? 'An error occurred.'); return; }
      if (result.comment) setComments(prev => [...prev, result.comment!]);
      setMessage('');
      setSent(true);
      setTimeout(() => bottomRef.current?.scrollIntoView({ behavior: 'smooth' }), 100);
    });
  };

  return (
    <div style={s.page}>
      {/* Header */}
      <div style={s.header}>
        <div style={s.headerInner}>
          <p style={s.label}>LegalSynq CareConnect</p>
          <h1 style={s.title}>Provider Referral Portal</h1>
        </div>
      </div>

      <div style={s.inner}>
        {/* Portal upgrade banner */}
        <div style={s.upgradeBox}>
          <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14, flexWrap: 'wrap' as const }}>
            <div style={{ flex: 1, minWidth: 200 }}>
              <p style={{ margin: '0 0 4px', fontSize: 14, fontWeight: 700, color: '#e0f2fe' }}>
                Manage all your referrals in one place
              </p>
              <p style={{ margin: 0, fontSize: 13, color: '#bfdbfe', lineHeight: 1.5 }}>
                Activate your CareConnect provider account to accept referrals, view patient details, track statuses, and collaborate — all from a single dashboard.
              </p>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column' as const, gap: 8, minWidth: 160 }}>
              <a href={activateUrl} style={{ ...s.btnPrimary, background: '#fff', color: '#1a56db', padding: '9px 16px', fontSize: 13 }}>
                Activate free account
              </a>
              <a href={loginUrl} style={{ ...s.btnPrimary, background: 'transparent', color: '#e0f2fe', border: '1px solid #60a5fa', padding: '8px 16px', fontSize: 13 }}>
                Already have access? Log in
              </a>
            </div>
          </div>
        </div>

        {/* Status + referral summary */}
        <div style={s.card}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14, flexWrap: 'wrap' as const, gap: 8 }}>
            <h2 style={{ margin: 0, fontSize: 15, fontWeight: 700, color: '#0f172a' }}>Referral Summary</h2>
            <span style={{
              background: st.bg, color: st.color,
              border: `1px solid ${st.border}`,
              borderRadius: 20, padding: '3px 12px', fontSize: 12, fontWeight: 600,
            }}>
              {st.label}
            </span>
          </div>
          <div style={s.grid2}>
            <FieldBlock label="Patient"      value={data.clientName} />
            <FieldBlock label="Service"      value={data.service} />
            <FieldBlock label="Referred by"  value={data.referrerName ?? '—'} />
            <FieldBlock label="Submitted"    value={formatDate(data.createdAt)} />
          </div>
        </div>

        {/* Accept / Decline — only shown while actionable */}
        {isActionable && (
          <div style={s.card}>
            <h2 style={s.cardTitle}>Your Response</h2>
            <p style={{ margin: '0 0 16px', fontSize: 13, color: '#6b7280' }}>
              Log in or activate your account to accept or decline this referral from within your secure provider dashboard.
            </p>
            <div style={{ display: 'flex', gap: 10 }}>
              <a href={loginUrl} style={{ ...s.btnPrimary, flex: 1 }}>Accept Referral</a>
              <a href={loginUrl} style={{ ...s.btnDanger, flex: 1 }}>Decline Referral</a>
            </div>
            <p style={{ margin: '10px 0 0', fontSize: 11, color: '#9ca3af', textAlign: 'center' as const }}>
              Requires secure login — your response is recorded in your dashboard.
            </p>
          </div>
        )}

        {/* Attachments */}
        {data.attachments && data.attachments.length > 0 && (
          <div style={s.card}>
            <h2 style={s.cardTitle}>Documents ({data.attachments.length})</h2>
            {data.attachments.map(att => {
              const dlUrl =
                `/api/public/careconnect/api/referrals/${referralId}/public-attachments/${att.id}/url` +
                `?token=${encodeURIComponent(token)}&download=true`;
              return (
                <a key={att.id} href={dlUrl} target="_blank" rel="noopener noreferrer" style={s.attRow}>
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#6b7280" strokeWidth="1.5">
                    <path strokeLinecap="round" strokeLinejoin="round"
                      d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <p style={{ margin: 0, fontSize: 13, fontWeight: 600, color: '#0f172a', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                      {att.fileName}
                    </p>
                    <p style={{ margin: 0, fontSize: 11, color: '#9ca3af' }}>{formatBytes(att.fileSizeBytes)}</p>
                  </div>
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#9ca3af" strokeWidth="2">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                  </svg>
                </a>
              );
            })}
          </div>
        )}

        {/* Message thread */}
        <div style={s.card}>
          <h2 style={s.cardTitle}>Messages</h2>
          {comments.length === 0 ? (
            <p style={{ margin: 0, fontSize: 14, color: '#94a3b8', fontStyle: 'italic' }}>
              No messages yet. Send the first message below.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              {comments.map(c => <CommentBubble key={c.id} comment={c} />)}
            </div>
          )}
          <div ref={bottomRef} />
        </div>

        {/* Send message form — provider side only */}
        <div style={s.card}>
          <h2 style={s.cardTitle}>Send a Message</h2>
          {sent && (
            <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 6, padding: '10px 14px', marginBottom: 14 }}>
              <p style={{ margin: 0, fontSize: 14, color: '#166534' }}>Message sent. The referring party will receive an email notification.</p>
            </div>
          )}
          {formError && (
            <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 6, padding: '10px 14px', marginBottom: 14 }}>
              <p style={{ margin: 0, fontSize: 14, color: '#991b1b' }}>{formError}</p>
            </div>
          )}
          <form onSubmit={handleSubmit}>
            <div style={{ marginBottom: 14 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>Your Name *</label>
              <input
                style={s.input}
                type="text"
                value={senderName}
                onChange={e => setSenderName(e.target.value)}
                placeholder="e.g. Dr. Jane Smith"
                maxLength={200}
                required
              />
            </div>
            <div style={{ marginBottom: 18 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>Message *</label>
              <textarea
                style={s.textarea}
                value={message}
                onChange={e => setMessage(e.target.value)}
                placeholder="Type your message here…"
                rows={4}
                maxLength={4000}
                required
              />
              <p style={{ margin: '4px 0 0', fontSize: 12, color: '#9ca3af', textAlign: 'right' as const }}>{message.length}/4000</p>
            </div>
            <button type="submit" disabled={isPending} style={{ ...s.btnPrimary, opacity: isPending ? 0.7 : 1, cursor: isPending ? 'not-allowed' : 'pointer' }}>
              {isPending ? 'Sending…' : 'Send Message'}
            </button>
          </form>
        </div>

        <p style={{ textAlign: 'center', marginTop: 8, marginBottom: 24, fontSize: 12, color: '#94a3b8' }}>
          Accessible only with the secure link from your referral email. This link expires 30 days from the referral date.
        </p>
      </div>
    </div>
  );
}

function FieldBlock({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p style={{ margin: '0 0 2px', fontSize: 11, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.05em' }}>{label}</p>
      <p style={{ margin: 0, fontSize: 14, color: '#0f172a', fontWeight: 500 }}>{value || '—'}</p>
    </div>
  );
}

function CommentBubble({ comment }: { comment: Comment }) {
  const isProvider = comment.senderType === 'provider';
  return (
    <div style={{ display: 'flex', flexDirection: isProvider ? 'row-reverse' : 'row', gap: 10, alignItems: 'flex-start' }}>
      <div style={{
        width: 34, height: 34, borderRadius: '50%', flexShrink: 0,
        background: isProvider ? '#dbeafe' : '#fef3c7',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 14, fontWeight: 700,
        color: isProvider ? '#1d4ed8' : '#92400e',
      }}>
        {comment.senderName.charAt(0).toUpperCase()}
      </div>
      <div style={{ maxWidth: '80%' }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexDirection: isProvider ? 'row-reverse' : 'row', marginBottom: 4 }}>
          <span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>{comment.senderName}</span>
          <span style={{ fontSize: 11, color: '#9ca3af' }}>{formatDate(comment.createdAt)}</span>
        </div>
        <div style={{
          background: isProvider ? '#eff6ff' : '#fafaf9',
          border: `1px solid ${isProvider ? '#bfdbfe' : '#e7e5e4'}`,
          borderRadius: isProvider ? '12px 4px 12px 12px' : '4px 12px 12px 12px',
          padding: '10px 14px',
        }}>
          <p style={{ margin: 0, fontSize: 14, color: '#111827', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>{comment.message}</p>
        </div>
      </div>
    </div>
  );
}
