'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import {
  addReviewCorrection,
  claimIntakeReview,
  completeIntakeReview,
  decideReviewDuplicate,
  decideReviewFinding,
  decideReviewMatch,
  getIntakeReview,
  type IntakeReviewWorkspace as Workspace,
} from '@/lib/intake-api';
import { ApiError } from '@/lib/api-client';

type Props = {
  reviewId: string;
  currentUserId?: string | null;
  canManage: boolean;
  canAssign: boolean;
  canComplete: boolean;
};

export function ReviewWorkspace({ reviewId, currentUserId, canManage, canAssign, canComplete }: Props) {
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [correction, setCorrection] = useState({ factCode: '', targetId: '', value: '', dataType: 'TEXT', reasonCode: 'HUMAN_CORRECTION', comment: '' });
  const [outcome, setOutcome] = useState('APPROVED');
  const [reasonCode, setReasonCode] = useState('');
  const [comment, setComment] = useState('');

  async function load() {
    setLoading(true);
    try {
      setWorkspace(await getIntakeReview(reviewId));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to load this review workspace.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, [reviewId]);

  async function run(action: () => Promise<unknown>) {
    setSaving(true);
    setError(null);
    try { await action(); await load(); }
    catch (err) { setError(err instanceof ApiError ? err.message : 'The action could not be saved.'); }
    finally { setSaving(false); }
  }

  if (loading) return <div className="min-h-full bg-slate-50 -m-6 p-6"><div className="mx-auto max-w-7xl space-y-4"><div className="h-8 w-48 animate-pulse rounded bg-slate-200" /><div className="h-96 animate-pulse rounded-xl bg-white" /></div></div>;
  if (!workspace) return <div className="m-6 rounded-xl border border-red-200 bg-red-50 p-6 text-sm text-red-700">{error ?? 'Review not found.'}<div className="mt-3"><Link className="font-medium underline" href="/intake">Back to Intake Center</Link></div></div>;

  const { review, source, classification, facts, matches, duplicates, findings, corrections } = workspace;
  const immutable = review.status === 'COMPLETED' || review.status === 'SUPERSEDED' || review.status === 'CANCELLED';
  const blocked = immutable || review.isStale || saving || !canManage;
  const unresolvedFindings = findings.filter((finding) => !finding.currentDecision);

  return (
    <div className="min-h-full bg-slate-50 -m-6 p-6">
      <div className="mx-auto max-w-7xl space-y-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <Link href="/intake" className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800"><i className="ri-arrow-left-line" /> Intake Center</Link>
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-semibold tracking-tight text-slate-950">Human review</h1>
              <StatusBadge value={review.status} />
              <PriorityBadge value={review.priority} />
            </div>
            <p className="mt-1 font-mono text-xs text-slate-500">Review {review.id} · Artifact {review.artifactId}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {canAssign && !immutable && !review.isStale && (
              <button type="button" disabled={saving} onClick={() => void run(() => claimIntakeReview(review.id, review.version))} className="rounded-lg bg-indigo-600 px-3.5 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-indigo-700 disabled:opacity-50">
                {review.assignedToUserId === currentUserId ? 'Continue review' : 'Claim review'}
              </button>
            )}
            <button type="button" onClick={() => void load()} className="rounded-lg border border-slate-200 bg-white px-3.5 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50"><i className="ri-refresh-line mr-1" /> Refresh</button>
          </div>
        </div>

        {error && <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}
        {review.isStale && <div className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800"><i className="ri-alert-line mt-0.5" /><div><strong className="font-semibold">This review is stale.</strong><p className="mt-0.5">B07–B11 produced a newer lineage. Decisions are disabled; return to the queue and open the current review.</p></div></div>}
        {immutable && <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-600"><i className="ri-lock-line mr-2" />This review is immutable because it is {labelize(review.status).toLowerCase()}.</div>}

        <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_360px]">
          <main className="space-y-5">
            <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
              <SectionHeader title="Source context" subtitle={`${labelize(source.sourceType)} intake · documents remain behind the Documents Service boundary`} icon="ri-file-shield-2-line" />
              <div className="grid gap-4 p-5 sm:grid-cols-2">
                <Info label="Received" value={source.receivedAt ? formatDate(source.receivedAt) : '—'} />
                <Info label="Sender" value={source.sender ?? '—'} />
                <Info label="Subject / title" value={source.emailSubject ?? source.manualTitle ?? '—'} />
                <Info label="Reference" value={source.manualReference ?? '—'} />
              </div>
              <div className="border-t border-slate-100 p-5">
                <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Documents</h3>
                <div className="mt-3 grid gap-2 sm:grid-cols-2">
                  {source.documents.map((document) => <div key={document.artifactId} className="flex items-center gap-3 rounded-lg border border-slate-200 p-3"><div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600"><i className="ri-file-text-line" /></div><div className="min-w-0"><div className="truncate text-sm font-medium text-slate-800">{document.fileName}</div><div className="text-[11px] text-slate-400">{document.contentType} · {formatBytes(document.sizeBytes)}</div></div>{document.reference && <span title="Documents Service reference" className="ml-auto text-emerald-600"><i className="ri-shield-check-line" /></span>}</div>)}
                </div>
              </div>
            </section>

            <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
              <SectionHeader title="Classification & extracted facts" subtitle="Compare the AI result with source evidence, then append a correction when necessary." icon="ri-sparkling-2-line" />
              <div className="p-5">
                {classification && <div className="mb-4 flex flex-wrap items-center gap-3 rounded-lg bg-slate-50 p-3"><span className="text-xs font-semibold uppercase tracking-wide text-slate-500">Classification</span><span className="font-medium text-slate-900">{classification.classificationLabel ?? classification.classificationCode ?? 'Unclassified'}</span><span className="rounded-full bg-white px-2 py-1 text-xs text-slate-600">{Math.round(classification.confidence * 100)}% confidence</span>{classification.wasOverridden && <span className="rounded-full bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700">Human override</span>}</div>}
                <div className="overflow-x-auto rounded-lg border border-slate-200">
                  <table className="min-w-full divide-y divide-slate-100 text-sm"><thead className="bg-slate-50 text-left text-[11px] uppercase tracking-wide text-slate-500"><tr><th className="px-3 py-2.5">Fact</th><th className="px-3 py-2.5">AI value</th><th className="px-3 py-2.5">Effective</th><th className="px-3 py-2.5">Quality</th></tr></thead><tbody className="divide-y divide-slate-100">{facts.map((fact) => <tr key={`${fact.factCode}-${fact.originalNormalizedFactId}`} className={fact.isRejected ? 'bg-red-50/40' : ''}><td className="px-3 py-3 font-mono text-xs font-medium text-slate-700">{fact.factCode}</td><td className="max-w-[220px] px-3 py-3 text-slate-600">{fact.isRejected ? <span className="text-red-600 line-through">Rejected</span> : fact.normalizedValue ?? fact.rawValue ?? '—'}</td><td className="max-w-[220px] px-3 py-3 text-slate-800">{fact.isRejected ? '—' : fact.effectiveValue ?? '—'}{fact.isHumanCorrected && <span className="ml-2 rounded bg-indigo-50 px-1.5 py-0.5 text-[10px] font-semibold text-indigo-700">HUMAN</span>}</td><td className="px-3 py-3 text-xs text-slate-500">{fact.validationStatus}<div className="text-[11px] text-slate-400">{Math.round(fact.sourceConfidence * 100)}% source confidence</div></td></tr>)}</tbody></table>
                </div>
              </div>
            </section>

            <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
              <SectionHeader title="Matching & duplicates" subtitle="Candidates are read-only B10 output; decisions are appended to this review." icon="ri-git-merge-line" />
              <div className="space-y-5 p-5">
                <div><h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Entity candidates</h3>{matches.length === 0 ? <Empty text="No candidates were persisted in the bound match run." /> : <div className="space-y-2">{matches.map((match) => <div key={match.id} className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 p-3"><div className="min-w-[160px] flex-1"><div className="text-xs font-semibold uppercase text-slate-500">{match.entityType}</div><div className="text-sm font-medium text-slate-900">{match.displayLabel}</div><div className="font-mono text-[10px] text-slate-400">{match.candidateEntityId}</div></div><span className="text-xs font-semibold text-slate-600">{Math.round(match.score * 100)}%</span>{canManage && !review.isStale && !immutable && <button type="button" disabled={saving} onClick={() => void run(() => decideReviewMatch(review.id, match.entityType, { artifactEntityMatchId: match.id, candidateEntityId: match.candidateEntityId, decision: 'CONFIRMED', reasonCode: 'HUMAN_CONFIRMED', version: review.version }))} className="rounded-md border border-emerald-200 px-2.5 py-1.5 text-xs font-medium text-emerald-700 hover:bg-emerald-50">Confirm</button>}</div>)}</div>}</div>
                <div><h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">Duplicate signals</h3>{duplicates.length === 0 ? <Empty text="No duplicate signals in this match run." /> : <div className="space-y-2">{duplicates.map((duplicate) => <div key={duplicate.id} className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 p-3"><div className="min-w-[160px] flex-1"><div className="text-sm font-medium text-slate-900">{labelize(duplicate.duplicateType)}</div><div className="text-xs text-slate-500">{duplicate.reasonCode} · {Math.round(duplicate.score * 100)}%</div></div>{canManage && !review.isStale && !immutable && <><button type="button" disabled={saving} onClick={() => void run(() => decideReviewDuplicate(review.id, duplicate.id, { decision: 'DUPLICATE_CONFIRMED', reasonCode: 'HUMAN_CONFIRMED', version: review.version }))} className="rounded-md border border-red-200 px-2.5 py-1.5 text-xs font-medium text-red-700 hover:bg-red-50">Duplicate</button><button type="button" disabled={saving} onClick={() => void run(() => decideReviewDuplicate(review.id, duplicate.id, { decision: 'NOT_DUPLICATE', reasonCode: 'HUMAN_REJECTED', version: review.version }))} className="rounded-md border border-slate-200 px-2.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Not duplicate</button></>}</div>)}</div>}</div>
              </div>
            </section>

            <section className="rounded-xl border border-slate-200 bg-white shadow-sm">
              <SectionHeader title="Policy findings" subtitle="Resolve review-level findings with an auditable reason. Blocking findings remain non-overridable." icon="ri-shield-check-line" />
              <div className="space-y-2 p-5">{findings.length === 0 ? <Empty text="No policy findings were recorded." /> : findings.map((finding) => <div key={finding.id} className="flex flex-wrap items-center gap-3 rounded-lg border border-slate-200 p-3"><div className="min-w-[180px] flex-1"><div className="flex items-center gap-2"><span className={`rounded px-1.5 py-0.5 text-[10px] font-semibold ${finding.severity === 'BLOCKING' ? 'bg-red-100 text-red-700' : 'bg-amber-50 text-amber-700'}`}>{finding.severity}</span><span className="font-mono text-xs text-slate-700">{finding.ruleCode}</span></div><div className="mt-1 text-xs text-slate-500">{finding.reasonCode}</div></div>{finding.currentDecision ? <span className="rounded-full bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700">{labelize(finding.currentDecision)}</span> : canManage && !review.isStale && !immutable && finding.severity !== 'BLOCKING' && <button type="button" disabled={saving} onClick={() => void run(() => decideReviewFinding(review.id, finding.id, { decision: 'RESOLVED', reasonCode: 'HUMAN_RESOLVED', version: review.version }))} className="rounded-md border border-emerald-200 px-2.5 py-1.5 text-xs font-medium text-emerald-700 hover:bg-emerald-50">Resolve</button>}</div>)}</div>
            </section>
          </main>

          <aside className="space-y-5">
            {canManage && !immutable && !review.isStale && <section className="rounded-xl border border-indigo-200 bg-indigo-50/60 p-5 shadow-sm"><h2 className="text-sm font-semibold text-indigo-950">Add a correction</h2><p className="mt-1 text-xs leading-5 text-indigo-800/75">Corrections are normalized deterministically and never mutate AI output.</p><div className="mt-4 space-y-3"><select value={correction.factCode} onChange={(e) => setCorrection((value) => ({ ...value, factCode: e.target.value, targetId: facts.find((fact) => fact.factCode === e.target.value)?.originalNormalizedFactId ?? '' }))} className="h-9 w-full rounded-lg border border-indigo-200 bg-white px-3 text-sm"><option value="">Select fact</option>{facts.filter((fact) => !fact.isRejected).map((fact) => <option key={fact.factCode} value={fact.factCode}>{fact.factCode}</option>)}</select><input value={correction.value} onChange={(e) => setCorrection((value) => ({ ...value, value: e.target.value }))} placeholder="Correct value" className="h-9 w-full rounded-lg border border-indigo-200 bg-white px-3 text-sm" /><textarea value={correction.comment} onChange={(e) => setCorrection((value) => ({ ...value, comment: e.target.value }))} placeholder="Why is this correction needed?" rows={3} className="w-full rounded-lg border border-indigo-200 bg-white px-3 py-2 text-sm" /><button type="button" disabled={blocked || !correction.factCode || !correction.value} onClick={() => void run(() => addReviewCorrection(review.id, { factCode: correction.factCode, targetId: correction.targetId || null, correctionType: 'VALUE_CORRECTION', correctedValue: correction.value, dataType: correction.dataType, reasonCode: correction.reasonCode, comment: correction.comment || null, humanVerified: true, version: review.version }))} className="w-full rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-50">Save correction</button></div></section>}

            <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="text-sm font-semibold text-slate-900">Review decision</h2><p className="mt-1 text-xs leading-5 text-slate-500">Complete only after the effective projection is safe for downstream B13 consumption.</p><div className="mt-4 space-y-3"><select value={outcome} onChange={(e) => setOutcome(e.target.value)} disabled={!canComplete || blocked} className="h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700"><option value="APPROVED">Approve</option><option value="APPROVED_WITH_CORRECTIONS">Approve with corrections</option><option value="DUPLICATE_CONFIRMED">Confirm duplicate</option><option value="NO_DUPLICATE">Not a duplicate</option><option value="NO_MATCH_CONFIRMED">Confirm no match</option><option value="REJECTED">Reject intake</option><option value="RETURN_FOR_REPROCESSING">Return for reprocessing</option></select><input value={reasonCode} onChange={(e) => setReasonCode(e.target.value)} disabled={!canComplete || blocked} placeholder="Reason code (required for reject / reprocess)" className="h-10 w-full rounded-lg border border-slate-200 px-3 text-sm disabled:bg-slate-50" /><textarea value={comment} onChange={(e) => setComment(e.target.value)} disabled={!canComplete || blocked} placeholder="Optional reviewer note" rows={3} className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm disabled:bg-slate-50" /><button type="button" disabled={!canComplete || blocked || ((outcome === 'REJECTED' || outcome === 'RETURN_FOR_REPROCESSING') && !reasonCode)} onClick={() => void run(() => completeIntakeReview(review.id, { outcome, reasonCode: reasonCode || null, comment: comment || null, version: review.version }))} className="w-full rounded-lg bg-slate-950 px-3 py-2.5 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-40">Complete review</button>{unresolvedFindings.length > 0 && <p className="text-[11px] text-amber-700">{unresolvedFindings.length} finding{unresolvedFindings.length === 1 ? '' : 's'} still need a decision.</p>}</div></section>

            <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm"><h2 className="text-sm font-semibold text-slate-900">Immutable history</h2><div className="mt-3 space-y-3">{corrections.slice(0, 6).map((item) => <div key={item.id} className="border-l-2 border-indigo-200 pl-3"><div className="text-xs font-medium text-slate-800">{labelize(item.correctionType)} · {item.factCode || 'classification'}</div><div className="mt-0.5 text-[11px] text-slate-500">{item.correctedValue ?? item.reasonCode}</div><div className="mt-1 text-[10px] text-slate-400">{formatDate(item.createdAt)}</div></div>)}{corrections.length === 0 && <Empty text="No human changes yet." />}</div></section>
          </aside>
        </div>
      </div>
    </div>
  );
}

function SectionHeader({ title, subtitle, icon }: { title: string; subtitle: string; icon: string }) { return <div className="flex items-start gap-3 border-b border-slate-100 px-5 py-4"><div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-600"><i className={icon} /></div><div><h2 className="text-sm font-semibold text-slate-900">{title}</h2><p className="mt-0.5 text-xs text-slate-500">{subtitle}</p></div></div>; }
function Info({ label, value }: { label: string; value: string }) { return <div><dt className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">{label}</dt><dd className="mt-1 truncate text-sm text-slate-700">{value}</dd></div>; }
function Empty({ text }: { text: string }) { return <div className="rounded-lg bg-slate-50 px-4 py-4 text-xs text-slate-500">{text}</div>; }
function labelize(value: string) { return value.replaceAll('_', ' ').toLowerCase().replace(/\b\w/g, (match) => match.toUpperCase()); }
function formatDate(value: string) { const date = new Date(value); return Number.isNaN(date.valueOf()) ? value : date.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }); }
function formatBytes(value: number) { if (value < 1024) return `${value} B`; if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`; return `${(value / (1024 * 1024)).toFixed(1)} MB`; }
function StatusBadge({ value }: { value: string }) { const tone = value === 'COMPLETED' ? 'bg-emerald-50 text-emerald-700' : value === 'IN_REVIEW' ? 'bg-indigo-50 text-indigo-700' : 'bg-amber-50 text-amber-700'; return <span className={`rounded-full px-2 py-1 text-[11px] font-semibold ${tone}`}>{labelize(value)}</span>; }
function PriorityBadge({ value }: { value: string }) { const tone = value === 'URGENT' ? 'bg-red-50 text-red-700' : value === 'HIGH' ? 'bg-orange-50 text-orange-700' : 'bg-slate-100 text-slate-600'; return <span className={`rounded-full px-2 py-1 text-[11px] font-semibold ${tone}`}>{labelize(value)}</span>; }