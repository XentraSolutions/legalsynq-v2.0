'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActionMenu } from '@/components/lien/action-menu';
import { UploadDocumentForm } from '@/components/lien/forms/upload-document-form';
import { ConfirmDialog } from '@/components/lien/modal';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatDate } from '@/lib/lien-mock-data';
import { DOCUMENT_CATEGORY_LABELS } from '@/types/lien';

export default function DocumentHandlingPage() {
  const documents = useLienStore((s) => s.documents);
  const updateDocument = useLienStore((s) => s.updateDocument);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [showUpload, setShowUpload] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ id: string; status: string; label: string } | null>(null);

  const filtered = documents.filter((d) => {
    if (search && !d.fileName.toLowerCase().includes(search.toLowerCase()) && !d.documentNumber.toLowerCase().includes(search.toLowerCase())) return false;
    if (statusFilter && d.status !== statusFilter) return false;
    if (categoryFilter && d.category !== categoryFilter) return false;
    return true;
  });

  const canEdit = canPerformAction(role, 'edit');

  return (
    <div className="space-y-5">
      <PageHeader title="Document Handling" subtitle={`${filtered.length} documents`}
        actions={canPerformAction(role, 'create') ? (
          <button onClick={() => setShowUpload(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-upload-2-line text-base" />Upload Document
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search documents..." onSearch={setSearch} filters={[
        { label: 'All Statuses', value: statusFilter, onChange: setStatusFilter, options: [{ value: 'Pending', label: 'Pending' }, { value: 'Processing', label: 'Processing' }, { value: 'Completed', label: 'Completed' }, { value: 'Failed', label: 'Failed' }, { value: 'Archived', label: 'Archived' }] },
        { label: 'All Categories', value: categoryFilter, onChange: setCategoryFilter, options: Object.entries(DOCUMENT_CATEGORY_LABELS).map(([v, l]) => ({ value: v, label: l })) },
      ]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Doc #</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">File Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Category</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Linked To</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Size</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Uploaded By</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Date</th>
              <th className="px-4 py-3" />
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((d) => (
                <tr key={d.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3"><Link href={`/lien/document-handling/${d.id}`} className="text-xs font-mono text-primary hover:underline">{d.documentNumber}</Link></td>
                  <td className="px-4 py-3"><div className="flex items-center gap-2"><i className="ri-file-text-line text-gray-400" /><span className="text-sm text-gray-700">{d.fileName}</span></div></td>
                  <td className="px-4 py-3 text-sm text-gray-600">{DOCUMENT_CATEGORY_LABELS[d.category] ?? d.category}</td>
                  <td className="px-4 py-3 text-xs text-gray-500">{d.linkedEntity}: {d.linkedEntityId}</td>
                  <td className="px-4 py-3 text-xs text-gray-400">{d.fileSize}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{d.uploadedBy}</td>
                  <td className="px-4 py-3"><StatusBadge status={d.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">{formatDate(d.createdAtUtc)}</td>
                  <td className="px-4 py-3 text-right">
                    <ActionMenu items={[
                      { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                      { label: 'Download', icon: 'ri-download-2-line', onClick: () => addToast({ type: 'info', title: 'Download', description: `${d.fileName} download simulated` }) },
                      ...(canEdit && d.status === 'Pending' ? [{ label: 'Mark Complete', icon: 'ri-checkbox-circle-line', onClick: () => setConfirmAction({ id: d.id, status: 'Completed', label: 'Complete Review' }) }] : []),
                      ...(canEdit && d.status !== 'Archived' ? [{ label: 'Archive', icon: 'ri-archive-line', onClick: () => setConfirmAction({ id: d.id, status: 'Archived', label: 'Archive Document' }), divider: true }] : []),
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No documents found.</div>}
      </div>

      <UploadDocumentForm open={showUpload} onClose={() => setShowUpload(false)} />

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateDocument(confirmAction.id, { status: confirmAction.status }); addToast({ type: 'success', title: confirmAction.label }); setConfirmAction(null); }}
          title={confirmAction.label} description={`${confirmAction.label} for this document?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
