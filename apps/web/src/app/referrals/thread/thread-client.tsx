'use client';

import { useState, useTransition, useRef } from 'react';
import { postComment } from './actions';

interface Comment {
  id: string;
  senderType: string;
  senderName: string;
  message: string;
  createdAt: string;
}

interface ThreadData {
  referralId: string;
  status: string;
  clientName: string;
  service: string;
  providerName: string;
  referrerName: string | null;
  createdAt: string;
  comments: Comment[];
}

interface Props {
  token: string;
  data: ThreadData;
}

function statusLabel(status: string): { text: string; color: string } {
  const map: Record<string, { text: string; color: string }> = {
    New:           { text: 'Awaiting Response',  color: '#f59e0b' },
    NewOpened:     { text: 'Opened by Provider', color: '#3b82f6' },
    Accepted:      { text: 'Accepted',           color: '#10b981' },
    Rejected:      { text: 'Declined',           color: '#ef4444' },
    Cancelled:     { text: 'Cancelled',          color: '#6b7280' },
    InProgress:    { text: 'In Progress',        color: '#8b5cf6' },
  };
  return map[status] ?? { text: status, color: '#6b7280' };
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit', hour12: true,
    });
  } catch {
    return iso;
  }
}

export function ThreadClient({ token, data }: Props) {
  const [comments, setComments]     = useState<Comment[]>(data.comments);
  const [senderType, setSenderType] = useState('');
  const [senderName, setSenderName] = useState('');
  const [message, setMessage]       = useState('');
  const [error, setError]           = useState('');
  const [sent, setSent]             = useState(false);
  const [isPending, startTransition] = useTransition();
  const bottomRef = useRef<HTMLDivElement>(null);

  const statusInfo = statusLabel(data.status);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSent(false);

    startTransition(async () => {
      const result = await postComment(token, senderType, senderName, message);
      if (!result.success) {
        setError(result.error ?? 'An error occurred.');
        return;
      }
      if (result.comment) {
        setComments(prev => [...prev, result.comment!]);
      }
      setMessage('');
      setSent(true);
      setTimeout(() => {
        bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
      }, 100);
    });
  };

  return (
    <div style={{ minHeight: '100vh', background: '#f8fafc', fontFamily: 'system-ui, -apple-system, sans-serif' }}>
      {/* Header */}
      <div style={{ background: '#0f172a', padding: '20px 24px', color: '#fff' }}>
        <div style={{ maxWidth: 680, margin: '0 auto' }}>
          <p style={{ margin: '0 0 4px', fontSize: 12, color: '#94a3b8', letterSpacing: '0.05em', textTransform: 'uppercase' }}>
            LegalSynq CareConnect
          </p>
          <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700 }}>Referral Thread</h1>
        </div>
      </div>

      <div style={{ maxWidth: 680, margin: '0 auto', padding: '24px 16px' }}>
        {/* Referral Summary Card */}
        <div style={{ background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0', padding: '20px 24px', marginBottom: 20 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: '#0f172a' }}>
              Referral Summary
            </h2>
            <span style={{
              display: 'inline-block',
              background: statusInfo.color + '18',
              color: statusInfo.color,
              border: `1px solid ${statusInfo.color}40`,
              borderRadius: 20,
              padding: '3px 12px',
              fontSize: 12,
              fontWeight: 600,
            }}>
              {statusInfo.text}
            </span>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px 24px' }}>
            <Field label="Patient" value={data.clientName} />
            <Field label="Service" value={data.service} />
            <Field label="Provider" value={data.providerName} />
            {data.referrerName && <Field label="Referring Party" value={data.referrerName} />}
            <Field label="Submitted" value={formatDate(data.createdAt)} />
          </div>
        </div>

        {/* Comment Thread */}
        <div style={{ background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0', padding: '20px 24px', marginBottom: 20 }}>
          <h2 style={{ margin: '0 0 16px', fontSize: 16, fontWeight: 700, color: '#0f172a' }}>
            Messages
          </h2>

          {comments.length === 0 ? (
            <p style={{ margin: 0, fontSize: 14, color: '#94a3b8', fontStyle: 'italic' }}>
              No messages yet. Use the form below to send the first message.
            </p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
              {comments.map(c => (
                <CommentBubble key={c.id} comment={c} />
              ))}
            </div>
          )}
          <div ref={bottomRef} />
        </div>

        {/* New Comment Form */}
        <div style={{ background: '#fff', borderRadius: 10, border: '1px solid #e2e8f0', padding: '20px 24px' }}>
          <h2 style={{ margin: '0 0 16px', fontSize: 16, fontWeight: 700, color: '#0f172a' }}>
            Send a Message
          </h2>

          {sent && (
            <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 6, padding: '10px 14px', marginBottom: 14 }}>
              <p style={{ margin: 0, fontSize: 14, color: '#166534' }}>
                Your message was sent. The other party will receive an email notification.
              </p>
            </div>
          )}

          {error && (
            <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 6, padding: '10px 14px', marginBottom: 14 }}>
              <p style={{ margin: 0, fontSize: 14, color: '#991b1b' }}>{error}</p>
            </div>
          )}

          <form onSubmit={handleSubmit}>
            <div style={{ marginBottom: 14 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                I am the *
              </label>
              <div style={{ display: 'flex', gap: 10 }}>
                <RoleButton
                  selected={senderType === 'referrer'}
                  onClick={() => setSenderType('referrer')}
                  label="Law Firm / Referrer"
                />
                <RoleButton
                  selected={senderType === 'provider'}
                  onClick={() => setSenderType('provider')}
                  label="Provider"
                />
              </div>
            </div>

            <div style={{ marginBottom: 14 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                Your Name *
              </label>
              <input
                type="text"
                value={senderName}
                onChange={e => setSenderName(e.target.value)}
                placeholder="e.g. Jane Smith"
                maxLength={200}
                style={{
                  width: '100%', boxSizing: 'border-box',
                  padding: '9px 12px', fontSize: 14,
                  border: '1px solid #d1d5db', borderRadius: 6,
                  outline: 'none', color: '#111827',
                }}
              />
            </div>

            <div style={{ marginBottom: 18 }}>
              <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                Message *
              </label>
              <textarea
                value={message}
                onChange={e => setMessage(e.target.value)}
                placeholder="Type your message here…"
                rows={4}
                maxLength={4000}
                style={{
                  width: '100%', boxSizing: 'border-box',
                  padding: '9px 12px', fontSize: 14,
                  border: '1px solid #d1d5db', borderRadius: 6,
                  outline: 'none', color: '#111827', resize: 'vertical',
                  fontFamily: 'inherit',
                }}
              />
              <p style={{ margin: '4px 0 0', fontSize: 12, color: '#9ca3af', textAlign: 'right' }}>
                {message.length}/4000
              </p>
            </div>

            <button
              type="submit"
              disabled={isPending}
              style={{
                background: isPending ? '#93c5fd' : '#1a56db',
                color: '#fff', border: 'none',
                padding: '10px 24px', borderRadius: 6,
                fontSize: 14, fontWeight: 700,
                cursor: isPending ? 'not-allowed' : 'pointer',
                width: '100%',
              }}
            >
              {isPending ? 'Sending…' : 'Send Message'}
            </button>
          </form>
        </div>

        <p style={{ textAlign: 'center', marginTop: 24, fontSize: 12, color: '#94a3b8' }}>
          This page is accessible only with the secure link from your referral email.
        </p>
      </div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p style={{ margin: '0 0 2px', fontSize: 11, fontWeight: 600, color: '#94a3b8', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
        {label}
      </p>
      <p style={{ margin: 0, fontSize: 14, color: '#0f172a', fontWeight: 500 }}>{value || '—'}</p>
    </div>
  );
}

function RoleButton({ selected, onClick, label }: { selected: boolean; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        flex: 1, padding: '8px 12px', fontSize: 13, fontWeight: 600,
        border: selected ? '2px solid #1a56db' : '2px solid #d1d5db',
        borderRadius: 6,
        background: selected ? '#eff6ff' : '#fff',
        color: selected ? '#1a56db' : '#374151',
        cursor: 'pointer',
      }}
    >
      {label}
    </button>
  );
}

function CommentBubble({ comment }: { comment: Comment }) {
  const isProvider = comment.senderType === 'provider';
  return (
    <div style={{
      display: 'flex',
      flexDirection: isProvider ? 'row-reverse' : 'row',
      gap: 10,
      alignItems: 'flex-start',
    }}>
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
        <div style={{
          display: 'flex', gap: 8, alignItems: 'baseline',
          flexDirection: isProvider ? 'row-reverse' : 'row',
          marginBottom: 4,
        }}>
          <span style={{ fontSize: 13, fontWeight: 600, color: '#374151' }}>{comment.senderName}</span>
          <span style={{ fontSize: 11, color: '#9ca3af' }}>{formatDate(comment.createdAt)}</span>
        </div>
        <div style={{
          background: isProvider ? '#eff6ff' : '#fafaf9',
          border: `1px solid ${isProvider ? '#bfdbfe' : '#e7e5e4'}`,
          borderRadius: isProvider ? '12px 4px 12px 12px' : '4px 12px 12px 12px',
          padding: '10px 14px',
        }}>
          <p style={{ margin: 0, fontSize: 14, color: '#111827', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>
            {comment.message}
          </p>
        </div>
      </div>
    </div>
  );
}
