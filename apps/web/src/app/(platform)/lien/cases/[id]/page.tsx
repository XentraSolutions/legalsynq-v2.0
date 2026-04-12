'use client';

import { use } from 'react';
import Link from 'next/link';
import { MOCK_CASE_DETAILS, MOCK_CASES, MOCK_LIENS, MOCK_DOCUMENTS, formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';

export default function CaseDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const detail = MOCK_CASE_DETAILS[id] ?? MOCK_CASES.find((c) => c.id === id);
  if (!detail) return <div className="p-10 text-center text-gray-400">Case not found.</div>;
  const d = { ...MOCK_CASES.find((c) => c.id === id), ...detail } as typeof detail & { description?: string; clientDob?: string; clientPhone?: string; clientEmail?: string; clientAddress?: string; insuranceCarrier?: string; policyNumber?: string; claimNumber?: string; demandAmount?: number; settlementAmount?: number; notes?: string };
  const caseLiens = MOCK_LIENS.filter((l) => l.caseRef === d.caseNumber);
  const caseDocs = MOCK_DOCUMENTS.filter((doc) => doc.linkedEntityId === d.caseNumber);

  return (
    <div className="space-y-5">
      <DetailHeader
        title={d.caseNumber}
        subtitle={d.clientName}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/cases"
        backLabel="Back to Cases"
        meta={[
          { label: 'Assigned', value: d.assignedTo },
          { label: 'Incident', value: formatDate(d.dateOfIncident) },
          { label: 'Created', value: formatDate(d.createdAtUtc) },
        ]}
        actions={
          <div className="flex gap-2">
            <button className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Edit</button>
            <button className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Update Status</button>
          </div>
        }
      />

      {d.description && (
        <div className="bg-white border border-gray-200 rounded-xl px-5 py-4">
          <p className="text-sm text-gray-600">{d.description}</p>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection
          title="Client Information"
          icon="ri-user-3-line"
          fields={[
            { label: 'Name', value: d.clientName },
            { label: 'Date of Birth', value: d.clientDob ? formatDate(d.clientDob) : undefined },
            { label: 'Phone', value: d.clientPhone },
            { label: 'Email', value: d.clientEmail },
            { label: 'Address', value: d.clientAddress },
          ]}
        />
        <DetailSection
          title="Case Details"
          icon="ri-folder-open-line"
          fields={[
            { label: 'Law Firm', value: d.lawFirm },
            { label: 'Medical Facility', value: d.medicalFacility },
            { label: 'Insurance Carrier', value: d.insuranceCarrier },
            { label: 'Policy Number', value: d.policyNumber },
            { label: 'Claim Number', value: d.claimNumber },
          ]}
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-1">Total Lien Amount</h3>
          <p className="text-2xl font-bold text-gray-900">{formatCurrency(d.totalLienAmount)}</p>
          <p className="text-xs text-gray-400 mt-1">{d.lienCount} liens attached</p>
        </div>
        {d.demandAmount && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h3 className="text-sm font-semibold text-gray-800 mb-1">Demand Amount</h3>
            <p className="text-2xl font-bold text-indigo-600">{formatCurrency(d.demandAmount)}</p>
          </div>
        )}
        {d.settlementAmount && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h3 className="text-sm font-semibold text-gray-800 mb-1">Settlement Amount</h3>
            <p className="text-2xl font-bold text-emerald-600">{formatCurrency(d.settlementAmount)}</p>
          </div>
        )}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h3 className="text-sm font-semibold text-gray-800">Related Liens ({caseLiens.length})</h3>
          <Link href="/lien/liens" className="text-xs text-primary font-medium hover:underline">View All Liens</Link>
        </div>
        {caseLiens.length > 0 ? (
          <div className="divide-y divide-gray-100">
            {caseLiens.map((lien) => (
              <Link key={lien.id} href={`/lien/liens/${lien.id}`} className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors">
                <div className="flex items-center gap-4">
                  <span className="text-xs font-mono text-gray-600">{lien.lienNumber}</span>
                  <span className="text-sm text-gray-700">{lien.lienType === 'MedicalLien' ? 'Medical Lien' : lien.lienType === 'AttorneyLien' ? 'Attorney Lien' : lien.lienType}</span>
                  <StatusBadge status={lien.status} />
                </div>
                <span className="text-sm font-medium text-gray-700 tabular-nums">{formatCurrency(lien.originalAmount)}</span>
              </Link>
            ))}
          </div>
        ) : (
          <div className="p-6 text-center text-sm text-gray-400">No liens attached to this case.</div>
        )}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <h3 className="text-sm font-semibold text-gray-800">Documents ({caseDocs.length})</h3>
          <Link href="/lien/document-handling" className="text-xs text-primary font-medium hover:underline">Document Handling</Link>
        </div>
        {caseDocs.length > 0 ? (
          <div className="divide-y divide-gray-100">
            {caseDocs.map((doc) => (
              <Link key={doc.id} href={`/lien/document-handling/${doc.id}`} className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors">
                <div className="flex items-center gap-3">
                  <i className="ri-file-text-line text-gray-400" />
                  <span className="text-sm text-gray-700">{doc.fileName}</span>
                  <StatusBadge status={doc.status} />
                </div>
                <span className="text-xs text-gray-400">{doc.fileSize}</span>
              </Link>
            ))}
          </div>
        ) : (
          <div className="p-6 text-center text-sm text-gray-400">No documents for this case.</div>
        )}
      </div>

      {d.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{d.notes}</p>
        </div>
      )}
    </div>
  );
}
