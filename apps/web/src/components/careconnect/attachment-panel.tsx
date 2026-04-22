'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import type { AttachmentSummary } from '@/types/careconnect';

// ── Props ─────────────────────────────────────────────────────────────────────

interface AttachmentPanelProps {
  entityType: 'referral' | 'appointment';
  entityId:   string;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes < 1024)       return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day:   'numeric',
    year:  'numeric',
  });
}

// ── Sub-components ────────────────────────────────────────────────────────────

interface ViewButtonProps {
  onView:  () => void;
  loading: boolean;
  error:   string | null;
}

function ViewButton({ onView, loading, error }: ViewButtonProps) {
  return (
    <div className="flex flex-col items-end gap-1">
      <button
        onClick={onView}
        disabled={loading}
        className="text-xs font-medium text-blue-600 hover:text-blue-800 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {loading ? 'Opening…' : 'View'}
      </button>
      {error && (
        <p className="text-xs text-red-600 max-w-[180px] text-right">{error}</p>
      )}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export function AttachmentPanel({ entityType, entityId }: AttachmentPanelProps) {
  const [attachments, setAttachments]     = useState<AttachmentSummary[]>([]);
  const [loadError,   setLoadError]       = useState<string | null>(null);
  const [uploading,   setUploading]       = useState(false);
  const [uploadError, setUploadError]     = useState<string | null>(null);

  // Per-attachment view state: attachmentId → { loading, error }
  const [viewState, setViewState] = useState<
    Record<string, { loading: boolean; error: string | null }>
  >({});

  const fileInputRef = useRef<HTMLInputElement>(null);

  // ── API helpers based on entityType ────────────────────────────────────────

  const apiList = useCallback(
    () =>
      entityType === 'referral'
        ? careConnectApi.referralAttachments.list(entityId)
        : careConnectApi.appointmentAttachments.list(entityId),
    [entityType, entityId],
  );

  const apiUpload = useCallback(
    (file: File) =>
      entityType === 'referral'
        ? careConnectApi.referralAttachments.upload(entityId, file)
        : careConnectApi.appointmentAttachments.upload(entityId, file),
    [entityType, entityId],
  );

  const apiGetSignedUrl = useCallback(
    (attachmentId: string) =>
      entityType === 'referral'
        ? careConnectApi.referralAttachments.getSignedUrl(entityId, attachmentId)
        : careConnectApi.appointmentAttachments.getSignedUrl(entityId, attachmentId),
    [entityType, entityId],
  );

  // ── Load attachments on mount ───────────────────────────────────────────────

  useEffect(() => {
    let cancelled = false;

    apiList()
      .then(({ data }) => {
        if (!cancelled) setAttachments(data);
      })
      .catch((err) => {
        if (!cancelled) {
          setLoadError(
            err instanceof ApiError
              ? err.message
              : 'Failed to load documents.',
          );
        }
      });

    return () => { cancelled = true; };
  }, [apiList]);

  // ── Upload handler ──────────────────────────────────────────────────────────

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploading(true);
    setUploadError(null);

    try {
      const { data: created } = await apiUpload(file);
      setAttachments((prev) => [...prev, created]);
    } catch (err) {
      setUploadError(
        err instanceof ApiError
          ? err.message
          : 'Upload failed. Please try again.',
      );
    } finally {
      setUploading(false);
      // Reset the input so the same file can be re-uploaded after an error
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  // ── View handler: fetches a fresh signed URL on every click ────────────────

  async function handleView(attachmentId: string) {
    setViewState((prev) => ({
      ...prev,
      [attachmentId]: { loading: true, error: null },
    }));

    try {
      const { data } = await apiGetSignedUrl(attachmentId);
      // Open in a new tab — do not persist the URL in state
      window.open(data.url, '_blank', 'noopener,noreferrer');
      setViewState((prev) => ({
        ...prev,
        [attachmentId]: { loading: false, error: null },
      }));
    } catch (err) {
      const message =
        err instanceof ApiError && err.isForbidden
          ? 'You do not have permission to view this document.'
          : err instanceof ApiError && err.isServerError
          ? 'The document is temporarily unavailable. Try again shortly.'
          : err instanceof ApiError
          ? err.message
          : 'Unable to open the document. Please try again.';

      setViewState((prev) => ({
        ...prev,
        [attachmentId]: { loading: false, error: message },
      }));
    }
  }

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
          Documents
        </h3>

        {/* Upload trigger */}
        <div>
          <input
            ref={fileInputRef}
            type="file"
            className="sr-only"
            id={`attachment-upload-${entityId}`}
            onChange={handleFileChange}
            disabled={uploading}
          />
          <label
            htmlFor={`attachment-upload-${entityId}`}
            className={[
              'inline-flex items-center gap-1 text-xs font-medium px-3 py-1.5 rounded',
              'bg-gray-100 text-gray-700 hover:bg-gray-200 transition-colors cursor-pointer',
              uploading ? 'opacity-50 pointer-events-none' : '',
            ].join(' ')}
          >
            {uploading ? 'Uploading…' : '+ Upload'}
          </label>
        </div>
      </div>

      {/* Upload error */}
      {uploadError && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">
          {uploadError}
        </div>
      )}

      {/* Load error */}
      {loadError && (
        <div className="mb-3 bg-red-50 border border-red-200 rounded px-3 py-2 text-xs text-red-700">
          {loadError}
        </div>
      )}

      {/* Attachment list */}
      {attachments.length === 0 && !loadError ? (
        <p className="text-sm text-gray-400 italic">No documents uploaded yet.</p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {attachments.map((a) => {
            const vs = viewState[a.id] ?? { loading: false, error: null };
            return (
              <li
                key={a.id}
                className="py-3 flex items-start justify-between gap-4"
              >
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-800 truncate">{a.fileName}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {formatBytes(a.fileSizeBytes)} · {formatDate(a.createdAtUtc)}
                    {a.notes && ` · ${a.notes}`}
                  </p>
                </div>

                <ViewButton
                  onView={() => handleView(a.id)}
                  loading={vs.loading}
                  error={vs.error}
                />
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
