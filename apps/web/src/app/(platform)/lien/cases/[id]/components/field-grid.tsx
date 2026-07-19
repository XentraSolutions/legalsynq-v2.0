import type { ReactNode } from "react";

export function FieldGrid({ children }: { children: ReactNode }) {
  return <dl className="grid grid-cols-3 gap-x-8 gap-y-4">{children}</dl>;
}

export function FieldItem({
  label,
  value,
}: {
  label: string;
  value?: string | null;
}) {
  return (
    <div>
      <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">
        {label}
      </dt>
      <dd className="text-sm text-gray-700 mt-1">{value || ""}</dd>
    </div>
  );
}
