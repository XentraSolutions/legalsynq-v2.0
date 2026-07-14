import type { ReactNode } from "react";

export function HeaderMeta({
  label,
  value,
  children,
}: {
  label: string;
  value?: string;
  children?: ReactNode;
}) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] text-gray-400 uppercase tracking-wide leading-tight">
        {label}
      </p>
      {children ? (
        <div className="mt-1">{children}</div>
      ) : (
        <p className="text-sm text-gray-700 font-medium mt-1 truncate">
          {value || "---"}
        </p>
      )}
    </div>
  );
}
