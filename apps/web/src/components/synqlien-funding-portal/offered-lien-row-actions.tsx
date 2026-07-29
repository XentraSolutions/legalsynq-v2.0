"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";

export function OfferedLienRowActions({
  lienNumber,
  detailHref,
}: {
  lienNumber: string;
  detailHref: string | null;
}) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    function handlePointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="relative inline-flex">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Open actions for ${lienNumber}`}
        onClick={() => setOpen(value => !value)}
        className="inline-flex h-8 w-8 items-center justify-center rounded-[8px] text-[#525252] transition-colors hover:bg-[#f5f5f5] hover:text-[#0a0a0a] focus:outline-none focus:ring-2 focus:ring-[#f4a076]"
      >
        <i className="ri-more-2-fill text-[20px]" />
      </button>

      {open ? (
        <div
          role="menu"
          className="absolute right-0 top-9 z-20 min-w-[128px] rounded-[10px] border border-[#e5e5e5] bg-white p-1 text-left shadow-[0_8px_24px_rgba(0,0,0,0.12)]"
        >
          {detailHref ? (
            <Link
              href={detailHref}
              role="menuitem"
              className="flex h-9 items-center gap-2 rounded-[8px] px-3 text-[14px] font-medium leading-[1.6] text-[#0a0a0a] transition-colors hover:bg-[#f5f5f5] focus:bg-[#f5f5f5] focus:outline-none"
            >
              <i className="ri-eye-line text-[16px] text-[#525252]" />
              View
            </Link>
          ) : (
            <span
              role="menuitem"
              aria-disabled="true"
              className="flex h-9 cursor-not-allowed items-center gap-2 rounded-[8px] px-3 text-[14px] font-medium leading-[1.6] text-[#a3a3a3]"
            >
              <i className="ri-eye-off-line text-[16px]" />
              View
            </span>
          )}
        </div>
      ) : null}
    </div>
  );
}
