'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { CASE_STATUS_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { NotesPanel } from '@/components/lien/notes-panel';
import { ConfirmDialog } from '@/components/lien/modal';
import { CreateLienModal } from '@/components/lien/forms/create-lien-modal';
import { UploadDocumentForm } from '@/components/lien/forms/upload-document-form';
import { AssignTaskForm } from '@/components/lien/forms/assign-task-form';

const CASE_STEPS = ['Pre-Demand', 'Demand Sent', 'In Negotiation', 'Settled', 'Closed'];
const STATUS_TO_STEP: Record<string, string> = { PreDemand: 'Pre-Demand', DemandSent: 'Demand Sent', InNegotiation: 'In Negotiation', CaseSettled: 'Settled', Closed: 'Closed' };

export default function CaseDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const getCaseDetail = useLienStore((s) => s.getCaseDetail);
  const liens = useLienStore((s) => s.liens);
  const documents = useLienStore((s) => s.documents);
  const updateCase = useLienStore((s) => s.updateCase);
  const addToast = useLienStore((s) => s.addToast);
  const addActivity = useLienStore((s) => s.addActivity);
  const caseNotes = useLienStore((s) => s.caseNotes[id] || []);
  const addCaseNote = useLienStore((s) => s.addCaseNote);
  const role = useLienStore((s) => s.currentRole);

  const [showAddLien, setShowAddLien] = useState(false);
  const [showAddDoc, setShowAddDoc] = useState(false);
  const [showAssignTask, setShowAssignTask] = useState(false);
  const [confirmStatus, setConfirmStatus] = useState<string | null>(null);

  const d = getCaseDetail(id) as any;
  if (!d) return <div className="p-10 text-center text-gray-400">Case not found.</div>;

  const relatedLiens = liens.filter((l) => l.caseRef === d.caseNumber);
  const relatedDocs = documents.filter((doc) => doc.linkedEntityId === d.caseNumber);
  const canEdit = canPerformAction(role, 'edit');

  const STATUSES = ['PreDemand', 'DemandSent', 'InNegotiation', 'CaseSettled', 'Closed'];
  const advanceStatus = () => {
    const idx = STATUSES.indexOf(d.status);
    if (idx < STATUSES.length - 1) setConfirmStatus(STATUSES[idx + 1]);
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
          { label: 'Law Firm', value: d.lawFirm },
          { label: 'Assigned', value: d.assignedTo },
          { label: 'Incident', value: formatDate(d.dateOfIncident) },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            <button onClick={advanceStatus} disabled={d.status === 'Closed'} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40">Advance Status</button>
            <button onClick={() => setShowAddLien(true)} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Add Lien</button>
            <button onClick={() => setShowAddDoc(true)} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Add Document</button>
            <button onClick={() => setShowAssignTask(true)} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Assign Task</button>
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
          <p className="text-xs text-gray-400 font-medium">Total Lien Amount</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{formatCurrency(d.totalLienAmount)}</p>
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
          { label: 'Date of Birth', value: d.clientDob ? formatDate(d.clientDob) : undefined },
          { label: 'Phone', value: d.clientPhone },
          { label: 'Email', value: d.clientEmail },
          { label: 'Address', value: d.clientAddress },
        ]} />
        <DetailSection title="Case Details" icon="ri-folder-open-line" fields={[
          { label: 'Law Firm', value: d.lawFirm },
          { label: 'Medical Facility', value: d.medicalFacility },
          { label: 'Insurance Carrier', value: d.insuranceCarrier },
          { label: 'Policy Number', value: d.policyNumber },
          { label: 'Claim Number', value: d.claimNumber },
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
            {canEdit && <button onClick={() => setShowAddLien(true)} className="text-xs text-primary font-medium hover:underline">+ Add Lien</button>}
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

      {relatedDocs.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800">Documents ({relatedDocs.length})</h3>
            {canEdit && <button onClick={() => setShowAddDoc(true)} className="text-xs text-primary font-medium hover:underline">+ Upload</button>}
          </div>
          <div className="divide-y divide-gray-100">
            {relatedDocs.map((doc) => (
              <Link key={doc.id} href={`/lien/document-handling/${doc.id}`} className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors">
                <div className="flex items-center gap-2">
                  <i className="ri-file-text-line text-gray-400" />
                  <span className="text-sm text-gray-700">{doc.fileName}</span>
                </div>
                <StatusBadge status={doc.status} />
              </Link>
            ))}
          </div>
        </div>
      )}

      <NotesPanel notes={caseNotes} onAddNote={(text) => addCaseNote(id, text)} readOnly={!canEdit} />

      <CreateLienModal open={showAddLien} onClose={() => setShowAddLien(false)} />
      <UploadDocumentForm open={showAddDoc} onClose={() => setShowAddDoc(false)} linkedEntity="Case" linkedEntityId={d.caseNumber} />
      <AssignTaskForm open={showAssignTask} onClose={() => setShowAssignTask(false)} caseNumber={d.caseNumber} />

      {confirmStatus && (
        <ConfirmDialog open onClose={() => setConfirmStatus(null)}
          onConfirm={() => {
            updateCase(id, { status: confirmStatus });
            addActivity({ type: 'case_update', description: `Case ${d.caseNumber} moved to ${CASE_STATUS_LABELS[confirmStatus]}`, actor: 'Current User', timestamp: new Date().toISOString(), icon: 'ri-folder-open-line', color: 'text-blue-600' });
            addToast({ type: 'success', title: 'Status Updated', description: `Case moved to ${CASE_STATUS_LABELS[confirmStatus]}` });
            setConfirmStatus(null);
          }}
          title="Advance Case Status" description={`Move ${d.caseNumber} to "${CASE_STATUS_LABELS[confirmStatus]}"?`} confirmLabel="Advance"
        />
      )}
    </div>
  );
}
