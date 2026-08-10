"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  titleClassName?: string;
  subtitle?: string;
  /** Extra controls rendered in the header, left of the close button (e.g. a "Clear Filter" action). */
  headerActions?: ReactNode;
  hasHeader?: boolean;
  children: ReactNode;
  footer?: ReactNode;
  size?: "sm" | "md" | "lg" | "xl";
}

const SIZE_MAP = {
  sm: "max-w-md",
  md: "max-w-lg",
  lg: "max-w-2xl",
  xl: "max-w-4xl",
};

export function Modal({
  open,
  onClose,
  title,
  titleClassName,
  subtitle,
  headerActions,
  hasHeader,
  children,
  footer,
  size = "md",
}: ModalProps) {
  const overlayRef = useRef<HTMLDivElement>(null);
  // Portaled to <body> below, so it always lands after (and therefore
  // paints above) any Radix-portaled popover/dropdown content, regardless
  // of where this Modal sits in the React tree. Deferred to a mounted
  // flag since `document` isn't available during SSR.
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handler);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", handler);
      document.body.style.overflow = "";
    };
  }, [open, onClose]);

  if (!open || !mounted) return null;

  return createPortal(
    <div
      ref={overlayRef}
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
      onClick={(e) => {
        if (e.target === overlayRef.current) onClose();
      }}
    >
      <div
        className="fixed inset-0 bg-black/40 backdrop-blur-sm"
        aria-hidden="true"
      />
      <div
        className={`relative bg-white rounded-xl shadow-xl w-full ${SIZE_MAP[size]} max-h-[90vh] flex flex-col animate-in fade-in zoom-in-95 duration-200`}
      >
        {hasHeader && (
          <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
            <div>
              <h2
                id="modal-title"
                className={`text-base font-semibold ${titleClassName ?? "text-gray-900"}`}
              >
                {title}
              </h2>
              {subtitle && (
                <p className="text-xs text-gray-500 mt-0.5">{subtitle}</p>
              )}
            </div>
            <div className="flex items-center gap-3 shrink-0">
              {headerActions}
              <button
                onClick={onClose}
                aria-label="Close dialog"
                className="p-1 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
              >
                <i className="ri-close-line text-xl" />
              </button>
            </div>
          </div>
        )}
        <div className="flex-1 overflow-y-auto px-6 py-4">{children}</div>
        {footer && (
          <div className="px-6 py-3 border-t border-gray-100 flex items-center justify-end gap-2">
            {footer}
          </div>
        )}
      </div>
    </div>,
    document.body,
  );
}

interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description: ReactNode;
  confirmLabel?: string;
  confirmVariant?: "primary" | "danger";
  loading?: boolean;
  warningTitle?: string;
  warningItems?: string[];
}

export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = "Confirm",
  confirmVariant = "primary",
  loading,
  warningTitle,
  warningItems,
}: ConfirmDialogProps) {
  const btnClass =
    confirmVariant === "danger"
      ? "bg-red-600 hover:bg-red-700 text-white"
      : "bg-primary hover:bg-[#EE7132]/90 text-white";

  return (
    <Modal
      open={open}
      onClose={loading ? () => {} : onClose}
      title={title}
      titleClassName={confirmVariant === "danger" ? "text-red-600" : undefined}
      size="sm"
      footer={
        <>
          <button
            onClick={onClose}
            disabled={loading}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            className={`text-sm px-4 py-2 rounded-lg ${btnClass} disabled:opacity-50`}
          >
            {loading ? "Processing..." : confirmLabel}
          </button>
        </>
      }
    >
      <p className="text-sm text-gray-600">{description}</p>
      {warningItems && warningItems.length > 0 && (
        <div className="mt-3 rounded-lg bg-gray-50 border border-gray-100 px-3 py-2.5">
          {warningTitle && (
            <p className="text-xs font-medium text-gray-400 mb-1.5">
              {warningTitle}
            </p>
          )}
          <ul className="space-y-1">
            {warningItems.map((item) => (
              <li key={item} className="text-xs text-gray-400">
                {item}
              </li>
            ))}
          </ul>
        </div>
      )}
    </Modal>
  );
}

interface FormModalProps {
  open: boolean;
  onClose: () => void;
  onSubmit: () => void;
  title: string;
  subtitle?: string;
  headerActions?: ReactNode;
  hasHeader?: boolean;
  children: ReactNode;
  submitLabel?: string;
  submitDisabled?: boolean;
  loading?: boolean;
  size?: "sm" | "md" | "lg" | "xl";
}

export function FormModal({
  open,
  onClose,
  onSubmit,
  title,
  subtitle,
  headerActions,
  children,
  submitLabel = "Save",
  submitDisabled,
  loading,
  size = "md",
  hasHeader = true,
}: FormModalProps) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      subtitle={subtitle}
      headerActions={headerActions}
      hasHeader={hasHeader}
      size={size}
      footer={
        <>
          <button
            onClick={onClose}
            className="flex-1 text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
          >
            Cancel
          </button>
          <button
            onClick={onSubmit}
            disabled={submitDisabled || loading}
            className="flex-1 text-sm px-4 py-2 bg-[#EE7132] hover:bg-[#EE7132]/90 text-white rounded-lg disabled:opacity-50"
          >
            {loading ? "Saving..." : submitLabel}
          </button>
        </>
      }
    >
      {children}
    </Modal>
  );
}
