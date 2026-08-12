"use client";

import { useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";

interface PanelShellProps {
  title: string;
  onEdit?: () => void;
  defaultExpanded?: boolean;
  children: ReactNode;
}

export function PanelShell({
  title,
  onEdit,
  defaultExpanded = true,
  children,
}: PanelShellProps) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="flex items-center justify-between px-6 py-4">
        <button
          onClick={() => setExpanded((v) => !v)}
          className="flex items-center gap-2 text-left"
        >
          <i
            className={`ri-arrow-${expanded ? "down" : "right"}-s-line text-gray-400`}
          />
          <h3 className="text-md font-semibold">{title}</h3>
        </button>
        {onEdit && (
          <div className="flex items-center border border-gray-200 rounded-lg overflow-hidden shrink-0">
            <Button
              variant="ghost"
              className="rounded-none shadow-none px-3 py-1.5 text-gray-700"
              onClick={onEdit}
            >
              Edit
            </Button>
            <Button
              variant="ghost"
              className="rounded-none shadow-none w-8 h-8 p-0 border-l border-gray-200 text-gray-700"
              onClick={onEdit}
              aria-label="Edit"
            >
              <i className="ri-edit-line text-sm" />
            </Button>
          </div>
        )}
      </div>
      {expanded && <div className="px-6 pb-5">{children}</div>}
    </div>
  );
}
