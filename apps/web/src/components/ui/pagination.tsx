"use client";

import { cn } from "@/lib/utils";

interface PaginationProps {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  className?: string;
  /** Overrides the default border-primary/bg-primary styling on the active page button (e.g. selling's orange brand). */
  primaryButtonClassName?: string;
}

const SIBLING_COUNT = 1;

function getPageItems(page: number, totalPages: number): (number | "ellipsis")[] {
  const totalNumbers = SIBLING_COUNT * 2 + 5;
  if (totalPages <= totalNumbers) {
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  }

  const leftSibling = Math.max(page - SIBLING_COUNT, 1);
  const rightSibling = Math.min(page + SIBLING_COUNT, totalPages);
  const showLeftEllipsis = leftSibling > 2;
  const showRightEllipsis = rightSibling < totalPages - 1;

  const items: (number | "ellipsis")[] = [1];

  if (showLeftEllipsis) items.push("ellipsis");
  for (let p = Math.max(leftSibling, 2); p <= Math.min(rightSibling, totalPages - 1); p++) {
    items.push(p);
  }
  if (showRightEllipsis) items.push("ellipsis");

  items.push(totalPages);

  return items;
}

function PageButton({
  active,
  disabled,
  onClick,
  children,
  ariaLabel,
  primaryButtonClassName,
}: {
  active?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  children: React.ReactNode;
  ariaLabel?: string;
  primaryButtonClassName?: string;
}) {
  return (
    <button
      type="button"
      aria-label={ariaLabel}
      aria-current={active ? "page" : undefined}
      disabled={disabled}
      onClick={onClick}
      className={cn(
        "inline-flex h-8 min-w-8 items-center justify-center rounded-lg border px-2.5 text-xs font-medium transition-colors",
        active
          ? (primaryButtonClassName ?? "border-primary bg-primary text-white")
          : "border-gray-200 text-gray-600 hover:bg-gray-50",
        disabled && "opacity-40 cursor-not-allowed hover:bg-transparent",
      )}
    >
      {children}
    </button>
  );
}

export function Pagination({ page, totalPages, onPageChange, className, primaryButtonClassName }: PaginationProps) {
  const items = getPageItems(page, totalPages);

  return (
    <div className={cn("flex items-center gap-1.5", className)}>
      <PageButton ariaLabel="First page" disabled={page <= 1} onClick={() => onPageChange(1)}>
        <i className="ri-arrow-left-double-line text-sm" />
      </PageButton>
      <PageButton ariaLabel="Previous page" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        <i className="ri-arrow-left-s-line text-sm" />
      </PageButton>

      {items.map((item, idx) =>
        item === "ellipsis" ? (
          <span key={`ellipsis-${idx}`} className="px-1 text-xs text-gray-400">
            …
          </span>
        ) : (
          <PageButton
            key={item}
            active={item === page}
            onClick={() => onPageChange(item)}
            primaryButtonClassName={primaryButtonClassName}
          >
            {item}
          </PageButton>
        ),
      )}

      <PageButton
        ariaLabel="Next page"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
      >
        <i className="ri-arrow-right-s-line text-sm" />
      </PageButton>
      <PageButton
        ariaLabel="Last page"
        disabled={page >= totalPages}
        onClick={() => onPageChange(totalPages)}
      >
        <i className="ri-arrow-right-double-line text-sm" />
      </PageButton>
    </div>
  );
}
