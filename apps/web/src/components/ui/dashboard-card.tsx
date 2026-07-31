import { ReactNode } from "react";

export function Card({
  title,
  actionLabel,
  className,
  children,
}: {
  title: string;
  actionLabel?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <article
      className={`flex flex-col rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] ${className ?? ""}`}
    >
      <div className="mb-6 flex items-center justify-between gap-4">
        <h2 className="text-base font-semibold leading-5">{title}</h2>
        {actionLabel ? (
          <button
            type="button"
            className="inline-flex h-9 items-center overflow-hidden rounded-[10px] border border-neutral-200 text-sm font-medium text-neutral-950 shadow-sm hover:bg-neutral-50"
          >
            <span className="px-4">{actionLabel}</span>
            <span className="inline-flex h-full w-9 items-center justify-center border-l border-neutral-200">
              <i className="ri-arrow-right-line" aria-hidden />
            </span>
          </button>
        ) : null}
      </div>
      <div className="flex flex-1 flex-col">{children}</div>
    </article>
  );
}
