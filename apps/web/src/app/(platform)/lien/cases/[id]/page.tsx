'use client';

import { use, useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { casesService, type CaseDetail, type CaseLienItem } from '@/lib/cases';
import { ApiError } from '@/lib/api-client';
import { StatusBadge } from '@/components/lien/status-badge';
import { NotesPanel } from '@/components/lien/notes-panel';
import { ConfirmDialog } from '@/components/lien/modal';
import { EntityTimeline } from '@/components/lien/entity-timeline';

const STATUS_LABELS: Record<string, string> = { PreDemand: 'Pre-demand', DemandSent: 'Demand Sent', InNegotiation: 'In Negotiation', CaseSettled: 'Case Settled', Closed: 'Closed' };
const STATUSES = ['PreDemand', 'DemandSent', 'InNegotiation', 'CaseSettled', 'Closed'];

const TABS = [
  { key: 'details', label: 'Details' },
  { key: 'liens', label: 'Liens' },
  { key: 'documents', label: 'Documents' },
  { key: 'servicing', label: 'Servicing' },
  { key: 'notes', label: 'Notes' },
  { key: 'taskmanager', label: 'Task Manager' },
] as const;

type TabKey = (typeof TABS)[number]['key'];

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '---';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

export default function CaseDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const caseNotes = useLienStore((s) => s.caseNotes[id] || []);

  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [relatedLiens, setRelatedLiens] = useState<CaseLienItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmStatus, setConfirmStatus] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<TabKey>('details');
  const [leftOpen, setLeftOpen] = useState(true);
  const [rightOpen, setRightOpen] = useState(true);

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

  const canEdit = ra.can('case:edit');

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

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="px-5 pt-3 pb-0 text-xs text-gray-400 flex items-center gap-1">
        <Link href="/lien/cases" className="hover:text-gray-600 transition-colors">Cases</Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">Liens Management</span>
      </div>

      <div className="bg-white border-b border-gray-200 px-6 py-4 mt-2">
        <div className="flex items-start justify-between gap-6">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold text-gray-900">{d.clientName}</h1>
            <p className="text-sm text-gray-400 mt-0.5">Case ID: {d.caseNumber}</p>
          </div>
          <div className="flex items-start gap-8 text-sm shrink-0">
            <div>
              <p className="text-xs text-gray-400 font-medium">Case Type</p>
              <p className="text-gray-700 mt-0.5">{d.title || '---'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400 font-medium">Case Status</p>
              <div className="mt-0.5"><StatusBadge status={d.status} /></div>
            </div>
            <div>
              <p className="text-xs text-gray-400 font-medium">Date of Loss</p>
              <p className="text-gray-700 mt-0.5">{d.dateOfIncident || '---'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400 font-medium">Date of Birth</p>
              <p className="text-gray-700 mt-0.5">{d.clientDob || '---'}</p>
            </div>
          </div>
        </div>
        <div className="flex items-end justify-between mt-3">
          <div className="flex items-center gap-8 text-sm">
            <div>
              <p className="text-xs text-gray-400 font-medium">State of Incident</p>
              <p className="text-gray-700 mt-0.5">{d.clientAddress ? d.clientAddress.split(',').pop()?.trim()?.split(' ')[0] || '---' : '---'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400 font-medium">Law Firm</p>
              <p className="text-gray-700 mt-0.5">{d.insuranceCarrier || '---'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-400 font-medium">Case Manager</p>
              <p className="text-gray-700 mt-0.5">---</p>
            </div>
          </div>
          {canEdit && (
            <button onClick={advanceStatus} disabled={d.status === 'Closed'}
              className="text-sm font-medium px-5 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 transition-colors">
              Actions
            </button>
          )}
        </div>
      </div>

      <div className="bg-white border-b border-gray-200 px-6">
        <nav className="flex gap-0 -mb-px">
          {TABS.map((tab) => (
            <button key={tab.key} onClick={() => setActiveTab(tab.key)}
              className={[
                'px-4 py-3 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
                activeTab === tab.key
                  ? 'border-primary text-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300',
              ].join(' ')}>
              {tab.label}
              {tab.key === 'liens' && (
                <span className="ml-1.5 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                  {relatedLiens.length}
                </span>
              )}
            </button>
          ))}
        </nav>
      </div>

      <div className="flex-1 min-h-0 overflow-auto bg-gray-50 p-5">
        {activeTab === 'details' && (
          <DetailsPanels d={d} leftOpen={leftOpen} rightOpen={rightOpen} setLeftOpen={setLeftOpen} setRightOpen={setRightOpen} />
        )}
        {activeTab === 'liens' && (
          <LiensTab liens={relatedLiens} />
        )}
        {activeTab === 'documents' && (
          <EmptyTab icon="ri-file-copy-2-line" label="Documents" />
        )}
        {activeTab === 'servicing' && (
          <EmptyTab icon="ri-tools-line" label="Servicing" />
        )}
        {activeTab === 'notes' && (
          <NotesPanel notes={caseNotes} onAddNote={() => {}} readOnly />
        )}
        {activeTab === 'taskmanager' && (
          <EmptyTab icon="ri-task-line" label="Task Manager" />
        )}
      </div>

      {confirmStatus && (
        <ConfirmDialog open onClose={() => setConfirmStatus(null)}
          onConfirm={confirmStatusChange}
          title="Advance Case Status" description={`Move ${d.caseNumber} to "${STATUS_LABELS[confirmStatus]}"?`} confirmLabel="Advance"
        />
      )}
    </div>
  );
}

function CollapsedStrip({ label, side, onExpand }: { label: string; side: 'left' | 'right'; onExpand: () => void }) {
  return (
    <div className="w-12 shrink-0 bg-white border border-gray-200 rounded-xl flex flex-col items-center pt-3 gap-2 self-stretch">
      <button onClick={onExpand} className="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-gray-100 text-gray-500 transition-colors">
        <i className={side === 'left' ? 'ri-arrow-right-double-line text-lg' : 'ri-arrow-left-double-line text-lg'} />
      </button>
      <span className="writing-vertical text-xs font-semibold text-primary tracking-wide" style={{ writingMode: 'vertical-rl', textOrientation: 'mixed' }}>
        {label}
      </span>
    </div>
  );
}

function PanelHeader({ title, side, onCollapse }: { title: string; side: 'left' | 'right'; onCollapse: () => void }) {
  return (
    <div className="flex items-center justify-between mb-4">
      <h2 className="text-base font-semibold text-gray-900 flex items-center gap-2">
        <button onClick={onCollapse} className="text-gray-400 hover:text-gray-600 transition-colors">
          <i className={side === 'left' ? 'ri-arrow-left-s-line text-lg' : 'ri-arrow-right-s-line text-lg'} />
        </button>
        {title}
      </h2>
    </div>
  );
}

function DetailsPanels({
  d, leftOpen, rightOpen, setLeftOpen, setRightOpen,
}: {
  d: CaseDetail;
  leftOpen: boolean;
  rightOpen: boolean;
  setLeftOpen: (v: boolean) => void;
  setRightOpen: (v: boolean) => void;
}) {
  return (
    <div className="flex gap-4 h-full min-h-[400px]">
      {leftOpen ? (
        <div className={`bg-white border border-gray-200 rounded-xl p-5 overflow-auto transition-all ${rightOpen ? 'flex-1 min-w-0' : 'flex-[2] min-w-0'}`}>
          <PanelHeader title="Plaintiff" side="left" onCollapse={() => setLeftOpen(false)} />

          <div className="bg-white border border-gray-200 rounded-xl p-5 mb-4">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-semibold text-gray-800 flex items-center gap-2">
                <span className="w-7 h-7 rounded-lg bg-blue-50 flex items-center justify-center">
                  <i className="ri-user-3-line text-sm text-blue-600" />
                </span>
                Plaintiff Info
              </h3>
              <button className="w-7 h-7 rounded-lg hover:bg-gray-100 flex items-center justify-center transition-colors">
                <i className="ri-pencil-line text-sm text-green-600" />
              </button>
            </div>
            <dl className="grid grid-cols-1 sm:grid-cols-3 gap-x-6 gap-y-4">
              <div className="sm:col-span-3">
                <dt className="text-xs font-semibold text-gray-900">Full Name</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.clientName || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Phone number</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.clientPhone || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Email</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.clientEmail || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Birthdate</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.clientDob || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Sex</dt>
                <dd className="text-sm text-gray-600 mt-0.5">---</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Address</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.clientAddress || '---'}</dd>
              </div>
            </dl>
          </div>

          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-semibold text-gray-800 flex items-center gap-2">
                <span className="w-7 h-7 rounded-lg bg-indigo-50 flex items-center justify-center">
                  <i className="ri-folder-open-line text-sm text-indigo-600" />
                </span>
                Case Details
              </h3>
            </div>
            <dl className="grid grid-cols-1 sm:grid-cols-3 gap-x-6 gap-y-4">
              <div>
                <dt className="text-xs font-semibold text-gray-900">External Reference</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.externalReference || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Insurance Carrier</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.insuranceCarrier || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Policy Number</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.policyNumber || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Claim Number</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.claimNumber || '---'}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Demand Amount</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{formatCurrency(d.demandAmount)}</dd>
              </div>
              <div>
                <dt className="text-xs font-semibold text-gray-900">Settlement Amount</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{formatCurrency(d.settlementAmount)}</dd>
              </div>
            </dl>
            {d.description && (
              <div className="mt-4 pt-4 border-t border-gray-100">
                <dt className="text-xs font-semibold text-gray-900">Description</dt>
                <dd className="text-sm text-gray-600 mt-0.5">{d.description}</dd>
              </div>
            )}
          </div>
        </div>
      ) : (
        <CollapsedStrip label="Details" side="left" onExpand={() => setLeftOpen(true)} />
      )}

      {rightOpen ? (
        <div className={`bg-white border border-gray-200 rounded-xl p-5 overflow-auto transition-all ${leftOpen ? 'w-[340px] shrink-0' : 'flex-1 min-w-0'}`}>
          <PanelHeader title="Email" side="right" onCollapse={() => setRightOpen(false)} />

          <button className="w-full flex items-center justify-center gap-2 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-3 transition-colors">
            <i className="ri-mail-send-line text-base" />
            Compose New Email
          </button>

          <div className="mt-6">
            <p className="text-sm text-gray-400 text-center py-8">No emails yet</p>
          </div>
        </div>
      ) : (
        <CollapsedStrip label="Email" side="right" onExpand={() => setRightOpen(true)} />
      )}
    </div>
  );
}

function LiensTab({ liens }: { liens: CaseLienItem[] }) {
  if (liens.length === 0) {
    return <EmptyTab icon="ri-stack-line" label="Liens" message="No liens linked to this case" />;
  }
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-100">
          <thead>
            <tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Amount</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {liens.map((l) => (
              <tr key={l.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-3">
                  <Link href={`/lien/liens/${l.id}`} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link>
                </td>
                <td className="px-4 py-3 text-sm text-gray-600">{l.lienType}</td>
                <td className="px-4 py-3 text-sm text-gray-700 font-medium tabular-nums">{formatCurrency(l.originalAmount)}</td>
                <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function EmptyTab({ icon, label, message }: { icon: string; label: string; message?: string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-10 text-center">
      <i className={`${icon} text-3xl text-gray-300`} />
      <p className="text-sm text-gray-400 mt-2">{message || `No ${label.toLowerCase()} data available`}</p>
    </div>
  );
}
