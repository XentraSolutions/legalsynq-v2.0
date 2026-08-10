"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/button";

// Selling's brand accent, matching the convention used on other selling pages.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: string;
  titleClassName?: string;
  subtitle?: ReactNode;
  /** Rendered left of the title (e.g. a small icon tile in a confirmation dialog). */
  icon?: ReactNode;
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
  icon,
  headerActions,
  hasHeader = true,
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
            <div className="flex items-center gap-3">
              {icon}
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
            </div>
            <div className="flex items-center gap-3 shrink-0">
              {headerActions}
              <Button
                variant="ghost"
                className="w-7 h-7 p-0 rounded-lg text-gray-400 hover:text-gray-600"
                onClick={onClose}
                aria-label="Close dialog"
              >
                <i className="ri-close-line text-xl" />
              </Button>
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
  /** Rendered left of the title (e.g. a small icon tile). */
  icon?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmVariant?: "primary" | "danger";
  /** Overrides the default bg-primary styling on the confirm button (e.g. selling's orange brand). */
  primaryButtonClassName?: string;
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
  icon,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  confirmVariant = "primary",
  primaryButtonClassName,
  loading,
  warningTitle,
  warningItems,
}: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      onClose={loading ? () => {} : onClose}
      title={title}
      icon={icon}
      titleClassName={confirmVariant === "danger" ? "text-red-600" : undefined}
      size="sm"
      footer={
        <>
          <Button variant="secondary" disabled={loading} onClick={onClose}>
            {cancelLabel}
          </Button>
          <Button
            variant={confirmVariant === "danger" ? "destructive" : "primary"}
            className={confirmVariant === "danger" ? undefined : primaryButtonClassName}
            loading={loading}
            onClick={onConfirm}
          >
            {loading ? "Processing..." : confirmLabel}
          </Button>
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
  subtitle?: ReactNode;
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
          <Button variant="secondary" className="flex-1" onClick={onClose}>
            Cancel
          </Button>
          <Button
            className={`flex-1 ${PRIMARY_BUTTON_CLASSNAME}`}
            disabled={submitDisabled || loading}
            loading={loading}
            onClick={onSubmit}
          >
            {loading ? "Saving..." : submitLabel}
          </Button>
        </>
      }
    >
      {children}
    </Modal>
  );
}
