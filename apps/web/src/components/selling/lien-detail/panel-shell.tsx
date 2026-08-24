"use client";

import { useState, type ReactNode } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { Button } from "@/components/selling/button";

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
          {expanded ? (
            <ChevronDown className="h-4 w-4 text-gray-400" />
          ) : (
            <ChevronRight className="h-4 w-4 text-gray-400" />
          )}
          <h3 className="text-md font-semibold">{title}</h3>
        </button>
        {onEdit && (
          <Button
            variant="secondary"
            rightIcon="squarePen"
            onClick={onEdit}
          >
            Edit
          </Button>
        )}
      </div>
      {expanded && <div className="px-6 pb-5">{children}</div>}
    </div>
  );
}
