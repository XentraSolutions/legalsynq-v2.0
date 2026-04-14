'use client';

import { use, useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { casesService, type CaseDetail, type CaseLienItem } from '@/lib/cases';
import { ApiError } from '@/lib/api-client';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { NotesPanel } from '@/components/lien/notes-panel';
import { ConfirmDialog } from '@/components/lien/modal';

const CASE_STEPS = ['Pre-Demand', 'Demand Sent', 'In Negotiation', 'Settled', 'Closed'];
const STATUS_TO_STEP: Record<string, string> = { PreDemand: 'Pre-Demand', DemandSent: 'Demand Sent', InNegotiation: 'In Negotiation', CaseSettled: 'Settled', Closed: 'Closed' };
const STATUS_LABELS: Record<string, string> = { PreDemand: 'Pre-Demand', DemandSent: 'Demand Sent', InNegotiation: 'In Negotiation', CaseSettled: 'Case Settled', Closed: 'Closed' };
const STATUSES = ['PreDemand', 'DemandSent', 'InNegotiation', 'CaseSettled', 'Closed'];

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '-';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

export default function CaseDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const role = useLienStore((s) => s.currentRole);
  const addToast = useLienStore((s) => s.addToast);
  const caseNotes = useLienStore((s) => s.caseNotes[id] || []);

  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [relatedLiens, setRelatedLiens] = useState<CaseLienItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmStatus, setConfirmStatus] = useState<string | null>(null);

  const fetchCase = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [detail, liensResult] = await Promise.all([
        casesService.getCase(id),
        casesService.getCaseLiens(id).catch(() => ({ items: [], pagination: { page: 1, pageSize: 50, totalCount: 0, totalPages: 0 } })),
      ]);
      setCaseDetail(detail);
      setRelatedLiens(liensResult.items);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.isNotFound ? 'Case not found.' : err.message);
      } else {
        setError('Failed to load case details');
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchCase();
  }, [fetchCase]);

  const canEdit = canPerformAction(role, 'edit');

  if (loading) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p className="text-sm text-gray-400 mt-2">Loading case details...</p>
      </div>
    );
  }

  if (error || !caseDetail) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">{error || 'Case not found.'}</p>
        <Link href="/lien/cases" className="text-sm text-primary hover:underline">Back to Cases</Link>
      </div>
    );
  }

  const d = caseDetail;

  const advanceStatus = () => {
    const idx = STATUSES.indexOf(d.status);
    if (idx < STATUSES.length - 1) setConfirmStatus(STATUSES[idx + 1]);
  };

  const confirmStatusChange = async () => {
    if (!confirmStatus) return;
    try {
      const updated = await casesService.updateCaseStatus(d.id, confirmStatus);
      setCaseDetail(updated);
      addToast({ type: 'success', title: 'Status Updated', description: `Case moved to ${STATUS_LABELS[confirmStatus]}` });
      setConfirmStatus(null);
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to update status';
      addToast({ type: 'error', title: 'Update Failed', description: message });
      setConfirmStatus(null);
    }
  };

  const nextStepHint = () => {
    switch (d.status) {
      case 'PreDemand': return 'Gather documentation and prepare demand letter';
      case 'DemandSent': return 'Awaiting response from insurance carrier';
      case 'InNegotiation': return 'Review counteroffers and negotiate settlement';
      case 'CaseSettled': return 'Process settlement distribution and close liens';
      default: return null;
    }
  };

  return (
    <div className="space-y-5">
      <DetailHeader title={d.caseNumber} subtitle={d.clientName}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/cases" backLabel="Back to Cases"
        meta={[
          { label: 'Title', value: d.title || '-' },
          { label: 'Insurance', value: d.insuranceCarrier || '-' },
          { label: 'Incident', value: d.dateOfIncident || '-' },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            <button onClick={advanceStatus} disabled={d.status === 'Closed'} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40">Advance Status</button>
          </div>
        ) : undefined}
      />

      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h3 className="text-sm font-semibold text-gray-800 mb-4">Case Workflow</h3>
        <StatusProgress steps={CASE_STEPS} currentStep={STATUS_TO_STEP[d.status] || 'Pre-Demand'} />
        {nextStepHint() && (
          <div className="mt-4 flex items-center gap-2 p-3 bg-blue-50 border border-blue-200 rounded-lg">
            <i className="ri-lightbulb-line text-blue-600" />
            <p className="text-xs text-blue-700"><span className="font-medium">Next step:</span> {nextStepHint()}</p>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Related Liens</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{relatedLiens.length}</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Demand Amount</p>
          <p className="text-2xl font-bold text-blue-600 mt-1">{formatCurrency(d.demandAmount)}</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Settlement Amount</p>
          <p className="text-2xl font-bold text-emerald-600 mt-1">{formatCurrency(d.settlementAmount)}</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Client Information" icon="ri-user-3-line" fields={[
          { label: 'Name', value: d.clientName },
          { label: 'Date of Birth', value: d.clientDob || undefined },
          { label: 'Phone', value: d.clientPhone || undefined },
          { label: 'Email', value: d.clientEmail || undefined },
          { label: 'Address', value: d.clientAddress || undefined },
        ]} />
        <DetailSection title="Case Details" icon="ri-folder-open-line" fields={[
          { label: 'Title', value: d.title || undefined },
          { label: 'External Reference', value: d.externalReference || undefined },
          { label: 'Insurance Carrier', value: d.insuranceCarrier || undefined },
          { label: 'Policy Number', value: d.policyNumber || undefined },
          { label: 'Claim Number', value: d.claimNumber || undefined },
        ]} />
      </div>

      {d.description && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Description</h3>
          <p className="text-sm text-gray-600">{d.description}</p>
        </div>
      )}

      {relatedLiens.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800">Related Liens ({relatedLiens.length})</h3>
          </div>
          <div className="divide-y divide-gray-100">
            {relatedLiens.map((l) => (
              <Link key={l.id} href={`/lien/liens/${l.id}`} className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors">
                <div>
                  <span className="text-xs font-mono text-primary">{l.lienNumber}</span>
                  <span className="text-sm text-gray-600 ml-2">{formatCurrency(l.originalAmount)}</span>
                </div>
                <StatusBadge status={l.status} />
              </Link>
            ))}
          </div>
        </div>
      )}

      <NotesPanel notes={caseNotes} onAddNote={() => {}} readOnly />

      {confirmStatus && (
        <ConfirmDialog open onClose={() => setConfirmStatus(null)}
          onConfirm={confirmStatusChange}
          title="Advance Case Status" description={`Move ${d.caseNumber} to "${STATUS_LABELS[confirmStatus]}"?`} confirmLabel="Advance"
        />
      )}
    </div>
  );
}
