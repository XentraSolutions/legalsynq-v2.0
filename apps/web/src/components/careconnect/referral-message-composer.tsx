'use client';

import { useRef, useState } from 'react';
import {
  CARECONNECT_MESSAGE_ALLOWED_TYPES,
  CARECONNECT_MESSAGE_MAX_FILES,
  formatCareConnectAttachmentBytes,
  makeSelectedCareConnectMessageFiles,
  type SelectedCareConnectMessageFile,
} from '@/lib/careconnect-message-attachments';

interface ReferralMessageComposerProps {
  message: string;
  onChange: (value: string) => void;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  isSubmitting: boolean;
  disabled?: boolean;
  files?: SelectedCareConnectMessageFile[];
  onFilesChange?: (files: SelectedCareConnectMessageFile[]) => void;
}

export function ReferralMessageComposer({
  message,
  onChange,
  onSubmit,
  isSubmitting,
  disabled = false,
  files = [],
  onFilesChange,
}: ReferralMessageComposerProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [fileError, setFileError] = useState<string | null>(null);
  const fileInputId = 'referral-message-attachments';
  const isDisabled = disabled || isSubmitting;

  function addFiles(incoming: File[]) {
    if (!onFilesChange) return;

    const result = makeSelectedCareConnectMessageFiles(incoming, files.length);
    setFileError(result.error);
    if (result.files.length > 0) {
      onFilesChange([...files, ...result.files]);
    }
  }

  function removeFile(id: string) {
    if (!onFilesChange) return;
    setFileError(null);
    onFilesChange(files.filter((selected) => selected.id !== id));
  }

  return (
    <form onSubmit={onSubmit} className="space-y-3">
      <div>
        <label
          htmlFor="referral-message"
          className="block text-xs font-semibold uppercase tracking-wider text-gray-500"
        >
          Send a message
        </label>
        <textarea
          id="referral-message"
          value={message}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Type your message here…"
          rows={4}
          maxLength={4000}
          disabled={isDisabled}
          className="mt-2 w-full rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-900 shadow-sm focus:border-blue-400 focus:outline-none focus:ring-2 focus:ring-blue-100 disabled:cursor-not-allowed disabled:bg-gray-50 disabled:text-gray-500"
        />
        <p className="mt-1 text-right text-xs text-gray-400">{message.length}/4000</p>
      </div>

      {onFilesChange && (
        <div className="space-y-2">
          <div
            role="button"
            tabIndex={isDisabled ? -1 : 0}
            aria-label="Attach files"
            onClick={() => !isDisabled && inputRef.current?.click()}
            onKeyDown={(e) => {
              if (!isDisabled && (e.key === 'Enter' || e.key === ' ')) {
                e.preventDefault();
                inputRef.current?.click();
              }
            }}
            onDragOver={(e) => {
              e.preventDefault();
              if (!isDisabled) setDragOver(true);
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={(e) => {
              e.preventDefault();
              setDragOver(false);
              if (!isDisabled) addFiles(Array.from(e.dataTransfer.files));
            }}
            className={[
              'flex items-center gap-2 rounded-md border px-3 py-2 text-sm transition-colors',
              dragOver
                ? 'border-blue-400 bg-blue-50'
                : 'border-dashed border-gray-300 bg-gray-50 hover:border-gray-400',
              isDisabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
            ].join(' ')}
          >
            <i className="ri-attachment-2 text-gray-400" aria-hidden="true" />
            <span className="text-gray-600">Attach files</span>
            <span className="ml-auto hidden text-xs text-gray-400 sm:inline">
              {CARECONNECT_MESSAGE_MAX_FILES} files · 50 MB each
            </span>
          </div>

          <input
            ref={inputRef}
            id={fileInputId}
            type="file"
            multiple
            accept={CARECONNECT_MESSAGE_ALLOWED_TYPES.join(',')}
            onChange={(e) => {
              addFiles(Array.from(e.target.files ?? []));
              e.target.value = '';
            }}
            disabled={isDisabled}
            className="hidden"
            aria-hidden="true"
            tabIndex={-1}
          />

          {files.length > 0 && (
            <ul className="space-y-1" aria-label="Selected message attachments">
              {files.map((selected) => (
                <li
                  key={selected.id}
                  className="flex items-center gap-2 rounded-md border border-gray-200 bg-gray-50 px-2.5 py-1.5 text-xs"
                >
                  <i className="ri-file-line shrink-0 text-gray-400" aria-hidden="true" />
                  <span className="min-w-0 flex-1 truncate text-gray-700">{selected.file.name}</span>
                  <span className="shrink-0 tabular-nums text-gray-400">
                    {formatCareConnectAttachmentBytes(selected.file.size)}
                  </span>
                  {!isDisabled && (
                    <button
                      type="button"
                      onClick={() => removeFile(selected.id)}
                      className="rounded p-0.5 text-gray-400 transition-colors hover:text-red-500"
                      aria-label={`Remove ${selected.file.name}`}
                    >
                      <i className="ri-close-line text-sm" aria-hidden="true" />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}

          {fileError && (
            <p className="text-xs text-red-600" role="alert">{fileError}</p>
          )}
        </div>
      )}

      <button
        type="submit"
        disabled={isDisabled}
        className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {isSubmitting ? 'Sending…' : 'Send Message'}
      </button>
    </form>
  );
}
