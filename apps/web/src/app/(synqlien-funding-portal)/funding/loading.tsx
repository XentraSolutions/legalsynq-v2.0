export default function SynqLienFundingLoading() {
  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <div className="h-4 w-28 rounded bg-slate-200" />
          <div className="mt-3 h-8 w-56 rounded bg-slate-200" />
        </div>
        <div className="h-10 w-32 rounded-md bg-slate-200" />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {Array.from({ length: 4 }).map((_, index) => (
          <div key={index} className="h-32 rounded-lg border border-slate-200 bg-white p-5">
            <div className="h-4 w-24 rounded bg-slate-200" />
            <div className="mt-6 h-8 w-28 rounded bg-slate-200" />
          </div>
        ))}
      </div>
      <div className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
        <div className="h-80 rounded-lg border border-slate-200 bg-white" />
        <div className="h-80 rounded-lg border border-slate-200 bg-white" />
      </div>
    </div>
  );
}
