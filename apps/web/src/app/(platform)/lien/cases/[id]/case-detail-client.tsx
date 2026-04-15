'use client';

import { useState, useEffect, useCallback, type ReactNode } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { casesService, type CaseDetail, type CaseLienItem } from '@/lib/cases';
import { ApiError } from '@/lib/api-client';
import { StatusBadge } from '@/components/lien/status-badge';
import { NotesPanel } from '@/components/lien/notes-panel';
import { ConfirmDialog } from '@/components/lien/modal';
import { LayoutSplit, type PanelMode } from '@/components/lien/layout-split';

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
  const [panelMode, setPanelMode] = useState<PanelMode>('split');

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
      <div className="px-6 pt-3 pb-0 text-xs text-gray-400 flex items-center gap-1">
        <Link href="/lien/cases" className="hover:text-gray-600 transition-colors">Cases</Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">Liens Management</span>
      </div>

      <div className="mx-6 mt-2 bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-6 py-4">
          <div className="flex items-center gap-8">
            <div className="shrink-0 min-w-[160px]">
              {/* TEMP: UI mock data for visual review only */}
              <h1 className="text-xl font-bold text-gray-900 leading-tight">{d.clientName || 'Maj Test'}</h1>
              <p className="text-xs text-gray-400 mt-1.5 font-medium">{d.caseNumber}</p>
            </div>

            <div className="flex-1 min-w-0">
              <div className="grid grid-cols-4 gap-x-6 gap-y-3">
                <HeaderMeta label="Case Type" value={d.title || 'Lien Case'} />
                <HeaderMeta label="Case Status">
                  <StatusBadge status={d.status} />
                </HeaderMeta>
                <HeaderMeta label="Date of Loss" value={d.dateOfIncident || '---'} />
                <HeaderMeta label="Date of Birth" value={d.clientDob || '---'} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="State of Incident" value="FL" />
                <HeaderMeta label="Law Firm" value={d.insuranceCarrier || 'Smith & Associates'} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="Case Manager" value="Sarah Mitchell" />
                {canEdit ? (
                  <div className="flex items-end">
                    <button onClick={advanceStatus} disabled={d.status === 'Closed'}
                      className="text-sm font-medium px-4 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 transition-colors whitespace-nowrap">
                      Actions
                    </button>
                  </div>
                ) : (
                  <div />
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="border-t border-gray-100 px-6">
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
      </div>

      <div className="flex-1 min-h-0 overflow-auto bg-gray-50 px-6 py-5">
        {activeTab === 'details' && (
          <DetailsTab d={d} panelMode={panelMode} onPanelModeChange={setPanelMode} />
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

function HeaderMeta({ label, value, children }: { label: string; value?: string; children?: ReactNode }) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] text-gray-400 uppercase tracking-wide leading-tight">{label}</p>
      {children ? (
        <div className="mt-1">{children}</div>
      ) : (
        <p className="text-sm text-gray-700 font-medium mt-1 truncate">{value || '---'}</p>
      )}
    </div>
  );
}

function CollapsibleSection({
  title,
  icon,
  defaultExpanded = true,
  onEdit,
  children,
}: {
  title: string;
  icon: string;
  defaultExpanded?: boolean;
  onEdit?: () => void;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div
        className="flex items-center justify-between px-5 py-3 cursor-pointer select-none hover:bg-gray-50/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <i className={`ri-arrow-${expanded ? 'down' : 'right'}-s-line text-gray-400 text-base`} />
          <i className={`${icon} text-sm text-gray-500`} />
          <h3 className="text-sm font-semibold text-gray-800">{title}</h3>
        </div>
        <div className="flex items-center gap-1">
          {onEdit && (
            <button
              onClick={(e) => { e.stopPropagation(); onEdit(); }}
              className="w-7 h-7 flex items-center justify-center rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
            >
              <i className="ri-pencil-line text-sm" />
            </button>
          )}
        </div>
      </div>
      {expanded && (
        <div className="px-5 py-4 border-t border-gray-100">{children}</div>
      )}
    </div>
  );
}

function FieldGrid({ children }: { children: ReactNode }) {
  return <dl className="grid grid-cols-2 gap-x-8 gap-y-4">{children}</dl>;
}

function FieldItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">{label}</dt>
      <dd className="text-sm text-gray-700 mt-1">{value || '---'}</dd>
    </div>
  );
}

function CheckboxField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-2.5 cursor-pointer group">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary/30 cursor-pointer"
      />
      <span className="text-sm text-gray-600 group-hover:text-gray-800 transition-colors select-none">{label}</span>
    </label>
  );
}

/* TEMP: visual fallback data for UI review only */
const TEMP_UPDATES = [
  { id: '1', timestamp: '04/14/2026 2:45 PM', action: 'Status Changed', description: 'Case moved from Pre-demand to Demand Sent', updatedBy: 'Sarah Mitchell' },
  { id: '2', timestamp: '04/10/2026 10:12 AM', action: 'Note Added', description: 'Follow-up scheduled with insurance adjuster', updatedBy: 'Sarah Mitchell' },
  { id: '3', timestamp: '04/05/2026 3:30 PM', action: 'Document Uploaded', description: 'Medical records package uploaded for review', updatedBy: 'James Rivera' },
  { id: '4', timestamp: '04/01/2026 9:00 AM', action: 'Case Created', description: 'New lien case opened for plaintiff', updatedBy: 'System' },
];

function DetailsTab({ d, panelMode, onPanelModeChange }: { d: CaseDetail; panelMode: PanelMode; onPanelModeChange: (m: PanelMode) => void }) {
  /* TEMP: visual fallback data for UI review only */
  const [shareWithLawFirm, setShareWithLawFirm] = useState(false);
  const [uccFiled, setUccFiled] = useState(false);
  const [caseDropped, setCaseDropped] = useState(false);
  const [childSupport, setChildSupport] = useState(false);
  const [minorComp, setMinorComp] = useState(false);

  const leftContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Plaintiff" icon="ri-user-line" onEdit={() => {}}>
        <div className="mb-3">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Plaintiff Info</p>
        </div>
        <FieldGrid>
          {/* TEMP: UI mock data for visual review only */}
          <FieldItem label="Full Name" value={d.clientName || 'Maj Test'} />
          <FieldItem label="Phone Number" value={d.clientPhone} />
          <FieldItem label="Email" value={d.clientEmail} />
          <FieldItem label="Birthdate" value={d.clientDob} />
          {/* TEMP: UI mock data for visual review only */}
          <FieldItem label="Sex" value="Male" />
          <FieldItem label="Address" value={d.clientAddress} />
        </FieldGrid>
      </CollapsibleSection>

      <CollapsibleSection title="Case Tracking" icon="ri-compass-3-line" onEdit={() => {}}>
        <div className="mb-3">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">Case Details</p>
        </div>
        <FieldGrid>
          {/* TEMP: UI mock data for visual review only */}
          <FieldItem label="Tracking Follow Up" value="04/20/2026" />
          <div>
            <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">Current Status</dt>
            <dd className="mt-1"><StatusBadge status={d.status} /></dd>
          </div>
          {/* TEMP: UI mock data for visual review only */}
          <FieldItem label="Current Medical Status" value="Active Treatment" />
          <FieldItem label="Case Type" value={d.title || 'Lien Case'} />
          {/* TEMP: UI mock data for visual review only */}
          <FieldItem label="State of Incident" value="FL" />
          {/* TEMP: UI mock data for visual review only */}
          <FieldItem label="Lead" value="Sarah Mitchell" />
        </FieldGrid>

        <div className="mt-4 pt-4 border-t border-gray-100">
          <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">Case Tracking Note</dt>
          {/* TEMP: UI mock data for visual review only */}
          <dd className="text-sm text-gray-600 mt-1.5 leading-relaxed">{d.description || 'Auto accident personal injury case involving multiple medical liens and ongoing treatment coordination with insurance carrier.'}</dd>
        </div>

        {/* TEMP: visual fallback data for UI review only */}
        <div className="mt-4 pt-4 border-t border-gray-100">
          <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight mb-3">Case Flags</p>
          <div className="grid grid-cols-3 gap-x-6 gap-y-2.5">
            <CheckboxField label="Share this case with Associated Law Firm" checked={shareWithLawFirm} onChange={setShareWithLawFirm} />
            <CheckboxField label="UCC Filed" checked={uccFiled} onChange={setUccFiled} />
            <CheckboxField label="Case Dropped" checked={caseDropped} onChange={setCaseDropped} />
            <CheckboxField label="Child Support" checked={childSupport} onChange={setChildSupport} />
            <CheckboxField label="Minor Comp" checked={minorComp} onChange={setMinorComp} />
          </div>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Updates" icon="ri-history-line">
        <div className="overflow-x-auto -mx-5 px-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100">
                <th className="pr-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Timestamp</th>
                <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Actions</th>
                <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">Description</th>
                <th className="pl-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">Updated By</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {/* TEMP: visual fallback data for UI review only */}
              {TEMP_UPDATES.map((u) => (
                <tr key={u.id} className="hover:bg-gray-50/50 transition-colors">
                  <td className="pr-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">{u.timestamp}</td>
                  <td className="px-4 py-2.5">
                    <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">{u.action}</span>
                  </td>
                  <td className="px-4 py-2.5 text-sm text-gray-600">{u.description}</td>
                  <td className="pl-4 py-2.5 text-sm text-gray-500 whitespace-nowrap">{u.updatedBy}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">Showing {TEMP_UPDATES.length} entries</p>
        </div>
      </CollapsibleSection>
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <i className="ri-user-line text-sm text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">Sarah Mitchell</p>
              <p className="text-xs text-gray-400">Case Manager</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">{d.insuranceCarrier || 'Smith & Associates'}</p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return <LayoutSplit left={leftContent} right={rightContent} mode={panelMode} onModeChange={onPanelModeChange} />;
}

function LiensTab({ liens }: { liens: CaseLienItem[] }) {
  if (liens.length === 0) {
    return <EmptyTab icon="ri-stack-line" label="Liens" message="No liens linked to this case" />;
  }
  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="bg-gray-50/80 border-b border-gray-100">
              <th className="px-5 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Lien #</th>
              <th className="px-5 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-5 py-2.5 text-right text-[11px] font-medium text-gray-500 uppercase tracking-wide">Amount</th>
              <th className="px-5 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {liens.map((l) => (
              <tr key={l.id} className="hover:bg-gray-50 transition-colors">
                <td className="px-5 py-2.5">
                  <Link href={`/lien/liens/${l.id}`} className="text-xs font-mono text-primary hover:underline">{l.lienNumber}</Link>
                </td>
                <td className="px-5 py-2.5 text-sm text-gray-600">{l.lienType}</td>
                <td className="px-5 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">{formatCurrency(l.originalAmount)}</td>
                <td className="px-5 py-2.5"><StatusBadge status={l.status} /></td>
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
    <div className="bg-white border border-gray-200 rounded-lg p-10 text-center">
      <i className={`${icon} text-3xl text-gray-300`} />
      <p className="text-sm text-gray-400 mt-2">{message || `No ${label.toLowerCase()} data available`}</p>
    </div>
  );
}
