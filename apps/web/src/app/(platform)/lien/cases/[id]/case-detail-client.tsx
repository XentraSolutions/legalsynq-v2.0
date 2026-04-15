'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { casesService, type CaseDetail, type CaseLienItem } from '@/lib/cases';
import { ApiError } from '@/lib/api-client';
import { StatusBadge } from '@/components/lien/status-badge';
import { NotesPanel } from '@/components/lien/notes-panel';
import { ConfirmDialog } from '@/components/lien/modal';
import {
  SplitPanelLayout,
  SectionCard,
  MetadataGrid,
  MetadataItem,
} from '@/components/lien/split-panel-layout';
import {
  MOCK_ACTIVITY,
  MOCK_CONTACTS,
  MOCK_CASE_SUMMARY,
  formatMockDateTime,
  type MockActivityItem,
  type MockContact,
} from '@/components/lien/case-mock-data';

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

const EMPTY_NOTES: { id: string; text: string; author: string; timestamp: string }[] = [];

export function CaseDetailClient({ id }: { id: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const caseNotes = useLienStore((s) => s.caseNotes[id] ?? EMPTY_NOTES);

  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [relatedLiens, setRelatedLiens] = useState<CaseLienItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmStatus, setConfirmStatus] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<TabKey>('details');

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
      {/* Breadcrumb */}
      <div className="px-6 pt-3 pb-0 text-xs text-gray-400 flex items-center gap-1">
        <Link href="/lien/cases" className="hover:text-gray-600 transition-colors">Cases</Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">{d.caseNumber}</span>
      </div>

      {/* Page Header */}
      <div className="bg-white border-b border-gray-200 px-6 py-4 mt-2">
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-3 min-w-0">
            <div className="min-w-0">
              <div className="flex items-center gap-2.5">
                <h1 className="text-lg font-semibold text-gray-900 truncate">{d.clientName || 'Test Case – Auto Claim'}</h1>
                <StatusBadge status={d.status} />
              </div>
              <p className="text-xs text-gray-400 mt-0.5">{d.caseNumber} · {d.title || 'Lien Case'}</p>
            </div>
          </div>
          <div className="flex items-center gap-6 shrink-0">
            <div className="hidden lg:flex items-center gap-6 text-sm">
              <div className="text-right">
                <p className="text-[11px] text-gray-400 uppercase tracking-wide">Date of Loss</p>
                <p className="text-sm text-gray-700 font-medium">{d.dateOfIncident || '---'}</p>
              </div>
              <div className="w-px h-8 bg-gray-200" />
              <div className="text-right">
                <p className="text-[11px] text-gray-400 uppercase tracking-wide">Carrier</p>
                <p className="text-sm text-gray-700 font-medium">{d.insuranceCarrier || 'Allstate Insurance'}</p>
              </div>
            </div>
            {canEdit && (
              <button onClick={advanceStatus} disabled={d.status === 'Closed'}
                className="text-sm font-medium px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 transition-colors">
                Advance Status
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Tab Bar */}
      <div className="bg-white border-b border-gray-200 px-6">
        <nav className="flex gap-0 -mb-px">
          {TABS.map((tab) => (
            <button key={tab.key} onClick={() => setActiveTab(tab.key)}
              className={[
                'px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap',
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

      {/* Tab Content */}
      <div className="flex-1 min-h-0 overflow-auto bg-gray-50 p-6">
        {activeTab === 'details' && (
          <SplitPanelLayout
            left={<LeftPanel d={d} liens={relatedLiens} />}
            right={<RightPanel d={d} liens={relatedLiens} />}
            leftLabel="Details"
            rightLabel="Summary"
          />
        )}
        {activeTab === 'liens' && <LiensTab liens={relatedLiens} />}
        {activeTab === 'documents' && <EmptyTab icon="ri-file-copy-2-line" label="Documents" />}
        {activeTab === 'servicing' && <EmptyTab icon="ri-tools-line" label="Servicing" />}
        {activeTab === 'notes' && <NotesPanel notes={caseNotes} onAddNote={() => {}} readOnly />}
        {activeTab === 'taskmanager' && <EmptyTab icon="ri-task-line" label="Task Manager" />}
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

/* ── Left Panel: Primary record details ── */
function LeftPanel({ d, liens }: { d: CaseDetail; liens: CaseLienItem[] }) {
  return (
    <div className="p-5 space-y-5">
      {/* Case Details */}
      <SectionCard title="Case Details" icon="ri-folder-open-line" iconBg="bg-indigo-50" iconColor="text-indigo-600">
        <MetadataGrid>
          <MetadataItem label="External Reference" value={d.externalReference} />
          <MetadataItem label="Insurance Carrier" value={d.insuranceCarrier || 'Allstate Insurance'} />
          <MetadataItem label="Policy Number" value={d.policyNumber} />
          <MetadataItem label="Claim Number" value={d.claimNumber} />
          <MetadataItem label="Demand Amount" value={formatCurrency(d.demandAmount)} />
          <MetadataItem label="Settlement Amount" value={formatCurrency(d.settlementAmount)} />
        </MetadataGrid>
        <div className="mt-3 pt-3 border-t border-gray-100">
          <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide">Description</dt>
          {/* TEMP: UI mock data for visual review only */}
          <dd className="text-sm text-gray-600 mt-0.5">{d.description || 'Auto accident personal injury case involving multiple medical liens and ongoing treatment coordination with insurance carrier.'}</dd>
        </div>
      </SectionCard>

      {/* Related Liens */}
      <SectionCard title="Related Liens" icon="ri-stack-line" iconBg="bg-purple-50" iconColor="text-purple-600"
        actions={
          <span className="text-xs text-gray-400 tabular-nums">{liens.length} lien{liens.length !== 1 ? 's' : ''}</span>
        }
      >
        {liens.length === 0 ? (
          <p className="text-sm text-gray-400 py-2">No liens linked to this case.</p>
        ) : (
          <div className="-mx-4 -mb-3">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-t border-gray-100">
                  <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Lien #</th>
                  <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Type</th>
                  <th className="px-4 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide">Amount</th>
                  <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {liens.map((l) => (
                  <tr key={l.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-4 py-2">
                      <Link href={`/lien/liens/${l.id}`} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link>
                    </td>
                    <td className="px-4 py-2 text-xs text-gray-600">{l.lienType}</td>
                    <td className="px-4 py-2 text-xs text-gray-700 font-medium tabular-nums text-right">{formatCurrency(l.originalAmount)}</td>
                    <td className="px-4 py-2"><StatusBadge status={l.status} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SectionCard>

      {/* Recent Activity */}
      {/* TEMP: UI mock data for visual review only */}
      <SectionCard title="Recent Activity" icon="ri-time-line" iconBg="bg-green-50" iconColor="text-green-600">
        <div className="space-y-3">
          {MOCK_ACTIVITY.slice(0, 5).map((item) => (
            <ActivityRow key={item.id} item={item} />
          ))}
        </div>
      </SectionCard>
    </div>
  );
}

/* ── Right Panel: Contextual summary ── */
function RightPanel({ d, liens }: { d: CaseDetail; liens: CaseLienItem[] }) {
  const totalLienAmount = liens.reduce((sum, l) => sum + (l.originalAmount || 0), 0);
  const summaryLiens = liens.length > 0 ? liens.length : MOCK_CASE_SUMMARY.totalLiens;
  const summaryAmount = liens.length > 0 ? totalLienAmount : MOCK_CASE_SUMMARY.totalLienAmount;

  return (
    <div className="p-5 space-y-5">
      {/* Case Summary */}
      <SectionCard title="Case Summary" icon="ri-bar-chart-box-line" iconBg="bg-amber-50" iconColor="text-amber-600">
        <div className="grid grid-cols-2 gap-3">
          <SummaryStatCard label="Total Liens" value={String(summaryLiens)} icon="ri-stack-line" />
          <SummaryStatCard label="Lien Amount" value={formatCurrency(summaryAmount)} icon="ri-money-dollar-circle-line" />
          {/* TEMP: UI mock data for visual review only */}
          <SummaryStatCard label="Documents" value={String(MOCK_CASE_SUMMARY.documentsCount)} icon="ri-file-copy-2-line" />
          <SummaryStatCard label="Open Tasks" value={String(MOCK_CASE_SUMMARY.openTasksCount)} icon="ri-task-line" />
        </div>
      </SectionCard>

      {/* Key Dates */}
      <SectionCard title="Key Dates" icon="ri-calendar-line" iconBg="bg-sky-50" iconColor="text-sky-600">
        <div className="space-y-2">
          <DateRow label="Date of Loss" value={d.dateOfIncident} />
          <DateRow label="Date of Birth" value={d.clientDob} />
          <DateRow label="Case Opened" value={d.openedAt} />
          <DateRow label="Case Closed" value={d.closedAt} />
          <DateRow label="Last Updated" value={d.updatedAt} />
        </div>
      </SectionCard>

      {/* Contacts */}
      {/* TEMP: UI mock data for visual review only */}
      <SectionCard title="Contacts" icon="ri-contacts-book-line" iconBg="bg-teal-50" iconColor="text-teal-600">
        <div className="space-y-3">
          {MOCK_CONTACTS.map((c) => (
            <ContactRow key={c.id} contact={c} />
          ))}
        </div>
      </SectionCard>
    </div>
  );
}

/* ── Subcomponents ── */

function SummaryStatCard({ label, value, icon }: { label: string; value: string; icon: string }) {
  return (
    <div className="bg-gray-50 rounded-lg px-3 py-2.5">
      <div className="flex items-center gap-1.5 mb-1">
        <i className={`${icon} text-xs text-gray-400`} />
        <span className="text-[11px] text-gray-400 uppercase tracking-wide">{label}</span>
      </div>
      <p className="text-sm font-semibold text-gray-800 tabular-nums">{value}</p>
    </div>
  );
}

function DateRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between py-1">
      <span className="text-xs text-gray-400">{label}</span>
      <span className="text-xs text-gray-700 font-medium tabular-nums">{value || '---'}</span>
    </div>
  );
}

function ActivityRow({ item }: { item: MockActivityItem }) {
  return (
    <div className="flex items-start gap-2.5">
      <div className="w-6 h-6 rounded-md bg-gray-50 flex items-center justify-center shrink-0 mt-0.5">
        <i className={`${item.icon} text-xs text-gray-500`} />
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm text-gray-700 leading-snug">{item.description}</p>
        <p className="text-[11px] text-gray-400 mt-0.5">{item.user} · {formatMockDateTime(item.timestamp)}</p>
      </div>
    </div>
  );
}

function ContactRow({ contact }: { contact: MockContact }) {
  return (
    <div className="flex items-center gap-3">
      <div className="w-7 h-7 rounded-full bg-gray-100 flex items-center justify-center shrink-0">
        <span className="text-[11px] font-semibold text-gray-500">
          {contact.name.split(' ').map(n => n[0]).join('')}
        </span>
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-sm text-gray-700 font-medium truncate">{contact.name}</p>
        <p className="text-[11px] text-gray-400">{contact.role}</p>
      </div>
    </div>
  );
}

/* ── Liens Tab ── */
function LiensTab({ liens }: { liens: CaseLienItem[] }) {
  if (liens.length === 0) {
    return <EmptyTab icon="ri-stack-line" label="Liens" message="No liens linked to this case" />;
  }
  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="bg-gray-50/80 border-b border-gray-100">
              <th className="px-4 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-2.5 text-right text-[11px] font-medium text-gray-500 uppercase tracking-wide">Amount</th>
              <th className="px-4 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {liens.map((l) => (
              <tr key={l.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-4 py-2.5">
                  <Link href={`/lien/liens/${l.id}`} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link>
                </td>
                <td className="px-4 py-2.5 text-sm text-gray-600">{l.lienType}</td>
                <td className="px-4 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">{formatCurrency(l.originalAmount)}</td>
                <td className="px-4 py-2.5"><StatusBadge status={l.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/* ── Empty Tab ── */
function EmptyTab({ icon, label, message }: { icon: string; label: string; message?: string }) {
  return (
    <div className="bg-white border border-gray-200 rounded-xl p-10 text-center">
      <i className={`${icon} text-3xl text-gray-300`} />
      <p className="text-sm text-gray-400 mt-2">{message || `No ${label.toLowerCase()} data available`}</p>
    </div>
  );
}
