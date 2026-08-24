import { useState, type ReactNode } from "react";

export function CollapsibleSection({
  title,
  icon,
  defaultExpanded = true,
  onEdit,
  children,
}: {
  title: string;
  icon: string;
  defaultExpanded?: boolean;
  onEdit?: () => void;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-visible">
      <div
        className="flex items-center justify-between px-5 py-3 cursor-pointer select-none hover:bg-gray-50/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <i
            className={`ri-arrow-${expanded ? "down" : "right"}-s-line text-gray-400 text-base`}
          />
          <i className={`${icon} text-sm text-gray-500`} />
          <h3 className="text-sm font-semibold text-gray-800">{title}</h3>
        </div>
        <div className="flex items-center gap-1">
          {onEdit && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onEdit();
              }}
              className="w-7 h-7 flex items-center justify-center rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
            >
              <i className="ri-pencil-line text-sm" />
            </button>
          )}
        </div>
      </div>
      {expanded && (
        <div className="px-5 py-4 border-t border-gray-100">{children}</div>
      )}
    </div>
  );
}
