'use client';

/**
 * ConfirmDialog — accessible modal confirmation dialog.
 *
 * Used for destructive or irreversible actions (suspend, deactivate, lock)
 * to give the admin a chance to confirm before the action is executed.
 *
 * ── Accessibility ─────────────────────────────────────────────────────────────
 *
 *   - role="dialog" + aria-modal="true" announces the dialog to screen readers
 *   - aria-labelledby points to the title element
 *   - aria-describedby points to the description element (when present)
 *   - Cancel button receives focus on mount — safer default for destructive actions
 *     (the admin must actively move to Confirm, reducing accidental confirmations)
 *   - Escape key closes the dialog (unless an action is pending)
 *   - Backdrop click closes the dialog (unless an action is pending)
 *   - All interactive elements have visible focus-visible rings
 *
 * ── Usage ─────────────────────────────────────────────────────────────────────
 *
 *   const [confirming, setConfirming] = useState(false);
 *
 *   {confirming && (
 *     <ConfirmDialog
 *       title="Suspend tenant?"
 *       description="The tenant will lose access immediately."
 *       confirmLabel="Suspend"
 *       variant="danger"
 *       onConfirm={() => { handleSuspend(); setConfirming(false); }}
 *       onCancel={() => setConfirming(false)}
 *     />
 *   )}
 */

import { useEffect, useRef, useId, type ReactNode } from 'react';

export interface ConfirmDialogProps {
  /** Short, imperative title. E.g. "Suspend tenant?" */
  title:         string;
  /** Optional longer explanation shown below the title. */
  description?:  ReactNode;
  /** Label on the confirm button. Default: "Confirm" */
  confirmLabel?: string;
  /** Label on the cancel button. Default: "Cancel" */
  cancelLabel?:  string;
  /**
   * Controls the colour of the confirm button:
   *   danger  — red  (destructive: suspend, delete, lock)
   *   warning — amber (caution: deactivate, reset)
   *   neutral — indigo (non-destructive confirm)
   * Default: 'neutral'
   */
  variant?:     'danger' | 'warning' | 'neutral' | 'success';
  /** Optional leading icon used by the spacious Figma dialog treatment. */
  icon?:        ReactNode;
  /** Uses the larger Control Center confirmation layout. */
  appearance?:  'default' | 'spacious';
  /** When true, both buttons are disabled and the confirm button shows a spinner. */
  isPending?:   boolean;
  /** Called when the admin clicks Confirm (or presses Enter while focused on it). */
  onConfirm:    () => void;
  /** Called when the admin clicks Cancel, clicks the backdrop, or presses Escape. */
  onCancel:     () => void;
}

export function ConfirmDialog({
  title,
  description,
  confirmLabel = 'Confirm',
  cancelLabel  = 'Cancel',
  variant      = 'neutral',
  icon,
  appearance   = 'default',
  isPending    = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const titleId       = useId();
  const descId        = useId();
  const cancelRef     = useRef<HTMLButtonElement>(null);
  const dialogRef     = useRef<HTMLDivElement>(null);

  // Auto-focus the cancel button on mount.
  // This ensures the admin must deliberately move focus to Confirm,
  // reducing accidental confirmations for destructive actions.
  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null;
    cancelRef.current?.focus();
    return () => previouslyFocused?.focus();
  }, []);

  // Close on Escape (guarded: don't close while action is in flight)
  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape' && !isPending) onCancel();
      if (e.key === 'Tab') {
        const focusable = Array.from(dialogRef.current?.querySelectorAll<HTMLElement>('button:not([disabled]), [href], input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])') ?? []);
        if (!focusable.length) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    }
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onCancel, isPending]);

  const confirmBtnStyles: Record<'danger' | 'warning' | 'neutral' | 'success', string> = {
    danger:  'bg-red-600 text-white hover:bg-red-700 focus-visible:ring-red-500',
    warning: 'bg-amber-600 text-white hover:bg-amber-700 focus-visible:ring-amber-500',
    neutral: 'bg-indigo-600 text-white hover:bg-indigo-700 focus-visible:ring-indigo-500',
    success: 'bg-[#22c55e] text-white hover:bg-[#16a34a] focus-visible:ring-[#22c55e]',
  };

  const spacious = appearance === 'spacious';

  return (
    /* Full-screen overlay */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center"
      aria-hidden={false}
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/40 backdrop-blur-[2px] transition-opacity"
        onClick={() => !isPending && onCancel()}
        aria-hidden="true"
      />

      {/* Dialog panel */}
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description ? descId : undefined}
        className={`relative z-10 mx-4 w-full border border-[#e5e5e5] bg-white shadow-[0_10px_15px_-3px_rgba(0,0,0,0.1),0_4px_6px_-4px_rgba(0,0,0,0.1)] ${spacious ? 'max-w-[512px] rounded-2xl' : 'max-w-sm rounded-xl p-6'}`}
      >
        <div className={spacious ? 'flex items-start gap-4 px-6 pb-4 pt-6' : ''}>
          {icon && <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] text-2xl ${variant === 'danger' ? 'bg-[#fef2f2] text-[#ef4444]' : 'bg-[#f5f5f5] text-[#0a0a0a]'}`}>{icon}</div>}
          <div className={spacious ? 'min-w-0 flex-1' : ''}>
            <h2 id={titleId} className={spacious ? 'pr-6 text-xl font-semibold leading-7 text-[#0a0a0a]' : 'text-sm font-semibold leading-snug text-gray-900'}>
              {title}
            </h2>
            {description && (
              <div id={descId} className={spacious ? 'mt-2 text-base leading-[1.6] text-[#737373]' : 'mt-1.5 text-xs leading-relaxed text-gray-500'}>
                {description}
              </div>
            )}
          </div>
          {spacious && (
            <button type="button" aria-label="Close dialog" disabled={isPending} onClick={onCancel} className="absolute right-3.5 top-3.5 flex h-6 w-6 items-center justify-center text-xl text-[#737373] hover:text-[#0a0a0a] disabled:opacity-50">
              <i className="ri-close-line" aria-hidden="true" />
            </button>
          )}
        </div>

        {/* Buttons */}
        <div className={spacious ? 'flex items-center justify-end gap-2 px-6 pb-6 pt-4' : 'mt-5 flex items-center justify-end gap-2'}>

          {/* Cancel */}
          <button
            ref={cancelRef}
            type="button"
            disabled={isPending}
            onClick={onCancel}
            className={[
              spacious ? 'rounded-[10px] px-4 py-2 text-sm font-medium transition-colors' : 'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
              'text-gray-700 bg-white border border-gray-300 hover:bg-gray-50',
              'focus:outline-none focus-visible:ring-2 focus-visible:ring-gray-400 focus-visible:ring-offset-1',
              'disabled:opacity-50 disabled:cursor-not-allowed',
            ].join(' ')}
          >
            {cancelLabel}
          </button>

          {/* Confirm */}
          <button
            type="button"
            disabled={isPending}
            onClick={onConfirm}
            className={[
              spacious ? 'rounded-[10px] px-4 py-2 text-sm font-medium transition-colors' : 'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
              'focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-1',
              'disabled:opacity-50 disabled:cursor-not-allowed',
              confirmBtnStyles[variant],
            ].join(' ')}
          >
            {isPending ? (
              <span className="flex items-center gap-1.5">
                <span
                  aria-hidden="true"
                  className="h-3.5 w-3.5 rounded-full border-2 border-white/60 border-t-transparent animate-spin"
                />
                Processing…
              </span>
            ) : (
              confirmLabel
            )}
          </button>

        </div>
      </div>
    </div>
  );
}
