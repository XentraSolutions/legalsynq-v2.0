"use client";

export default function SynqLienFundingError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="rounded-lg border border-rose-200 bg-white p-6 shadow-sm">
      <div className="flex items-start gap-3">
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md bg-rose-50 text-rose-600">
          <i className="ri-error-warning-line text-[18px]" />
        </span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold text-slate-950">Unable to load this portal view</h2>
          <p className="mt-1 text-sm text-slate-500">
            {error.message || 'The request failed. Please try again.'}
          </p>
          <button
            type="button"
            onClick={reset}
            className="mt-4 inline-flex h-9 items-center gap-2 rounded-md bg-slate-950 px-3 text-sm font-medium text-white transition-colors hover:bg-slate-800"
          >
            <i className="ri-refresh-line text-[15px]" />
            Retry
          </button>
        </div>
      </div>
    </div>
  );
}
