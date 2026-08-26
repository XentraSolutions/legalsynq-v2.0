import { ReactNode } from "react";

export function Card({
  title,
  subtitle,
  icon,
  actionLabel,
  className,
  children,
}: {
  title: string;
  subtitle?: string;
  /** Remix Icon class rendered before the title, e.g. "ri-draggable". */
  icon?: string;
  actionLabel?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <article
      className={`flex flex-col rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] ${className ?? ""}`}
    >
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="flex items-center gap-2 text-base font-semibold leading-5">
            {icon && <i className={`${icon} text-gray-400`} aria-hidden />}
            {title}
          </h2>
          {subtitle && <p className="mt-1 text-xs text-gray-500">{subtitle}</p>}
        </div>
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
