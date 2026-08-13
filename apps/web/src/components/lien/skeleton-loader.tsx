'use client';

export function SkeletonRow({ cols = 5 }: { cols?: number }) {
  return (
    <tr className="animate-pulse">
      {Array.from({ length: cols }).map((_, i) => (
        <td key={i} className="px-4 py-3">
          <div className="h-4 bg-gray-100 rounded w-3/4" />
        </td>
      ))}
    </tr>
  );
}

export function SkeletonTable({ rows = 5, cols = 5 }: { rows?: number; cols?: number }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <table className="min-w-full">
        <thead>
          <tr className="bg-gray-50">
            {Array.from({ length: cols }).map((_, i) => (
              <th key={i} className="px-4 py-3"><div className="h-3 bg-gray-200 rounded w-20 animate-pulse" /></th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {Array.from({ length: rows }).map((_, i) => <SkeletonRow key={i} cols={cols} />)}
        </tbody>
      </table>
    </div>
  );
}

export function SkeletonCard() {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-5 animate-pulse space-y-3">
      <div className="h-3 bg-gray-100 rounded w-1/3" />
      <div className="h-6 bg-gray-100 rounded w-1/2" />
      <div className="h-3 bg-gray-100 rounded w-2/3" />
    </div>
  );
}

export function SkeletonField({ full = false }: { full?: boolean }) {
  return (
    <div className={`space-y-2 animate-pulse ${full ? "col-span-full" : ""}`}>
      <div className="h-3 bg-gray-100 rounded w-24" />
      <div className="h-9 bg-gray-100 rounded-lg w-full" />
    </div>
  );
}

export function SkeletonFormGrid({
  fields = 4,
  className = "grid-cols-1 sm:grid-cols-2",
}: {
  fields?: number;
  className?: string;
}) {
  return (
    <div className={`grid ${className} gap-4`}>
      {Array.from({ length: fields }).map((_, i) => (
        <SkeletonField key={i} />
      ))}
    </div>
  );
}

export function SkeletonListRows({ rows = 4 }: { rows?: number }) {
  return (
    <div className="border border-gray-200 rounded-lg divide-y divide-gray-100 animate-pulse">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex items-center gap-3 px-4 py-3">
          <div className="h-4 w-4 rounded-full bg-gray-200 shrink-0" />
          <div className="h-4 bg-gray-100 rounded w-1/2" />
        </div>
      ))}
    </div>
  );
}

export function SkeletonFileRow() {
  return (
    <div className="flex items-center gap-3 px-4 py-3 border border-gray-200 rounded-lg animate-pulse">
      <div className="h-9 w-9 rounded-lg bg-gray-100 shrink-0" />
      <div className="flex-1 space-y-2">
        <div className="h-3.5 bg-gray-100 rounded w-1/3" />
        <div className="h-3 bg-gray-100 rounded w-1/4" />
      </div>
      <div className="h-8 w-8 rounded-lg bg-gray-100 shrink-0" />
    </div>
  );
}

export function SkeletonDetail() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="bg-white border border-gray-200 rounded-xl px-6 py-5 space-y-3">
        <div className="h-3 bg-gray-100 rounded w-20" />
        <div className="h-6 bg-gray-200 rounded w-48" />
        <div className="flex gap-4"><div className="h-3 bg-gray-100 rounded w-32" /><div className="h-3 bg-gray-100 rounded w-32" /></div>
      </div>
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <div className="bg-white border border-gray-200 rounded-xl p-5 space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (<div key={i} className="h-4 bg-gray-100 rounded w-full" />))}
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5 space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (<div key={i} className="h-4 bg-gray-100 rounded w-full" />))}
        </div>
      </div>
    </div>
  );
}
