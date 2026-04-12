'use client';

import { use, useState } from 'react';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatDate } from '@/lib/lien-mock-data';
import { DOCUMENT_CATEGORY_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { ConfirmDialog } from '@/components/lien/modal';

export default function DocumentDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const documents = useLienStore((s) => s.documents);
  const documentDetails = useLienStore((s) => s.documentDetails);
  const updateDocument = useLienStore((s) => s.updateDocument);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [confirmAction, setConfirmAction] = useState<{ status: string; label: string } | null>(null);

  const summary = documents.find((d) => d.id === id);
  const detail = documentDetails[id];
  const doc = detail ? { ...summary, ...detail } : summary;
  if (!doc) return <div className="p-10 text-center text-gray-400">Document not found.</div>;
  const d = doc as any;
  const canEdit = canPerformAction(role, 'edit');

  return (
    <div className="space-y-5">
      <DetailHeader title={d.documentNumber} subtitle={d.fileName}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/document-handling" backLabel="Back to Documents"
        meta={[
          { label: 'Category', value: DOCUMENT_CATEGORY_LABELS[d.category] ?? d.category },
          { label: 'Size', value: d.fileSize },
          { label: 'Uploaded', value: formatDate(d.createdAtUtc) },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            <button onClick={() => addToast({ type: 'info', title: 'Download', description: `${d.fileName} download simulated` })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"><i className="ri-download-2-line mr-1" />Download</button>
            {d.status === 'Pending' && <button onClick={() => setConfirmAction({ status: 'Completed', label: 'Complete Review' })} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Complete Review</button>}
            {d.status !== 'Archived' && <button onClick={() => setConfirmAction({ status: 'Archived', label: 'Archive' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Archive</button>}
            <button onClick={() => addToast({ type: 'info', title: 'Share', description: 'Sharing simulated' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"><i className="ri-share-line mr-1" />Share</button>
          </div>
        ) : undefined}
      />

      <div className="bg-white border border-gray-200 rounded-xl p-8">
        <div className="border-2 border-dashed border-gray-200 rounded-lg p-16 text-center">
          <i className="ri-file-text-line text-6xl text-gray-300 mb-4" />
          <p className="text-sm font-medium text-gray-500">Document Preview</p>
          <p className="text-xs text-gray-400 mt-1">{d.fileName}</p>
          <button onClick={() => addToast({ type: 'info', title: 'Preview', description: 'Full preview simulated' })} className="mt-4 text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">Open Full Preview</button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Document Metadata" icon="ri-information-line" fields={[
          { label: 'Document Number', value: d.documentNumber },
          { label: 'File Name', value: d.fileName },
          { label: 'Category', value: DOCUMENT_CATEGORY_LABELS[d.category] ?? d.category },
          { label: 'MIME Type', value: d.mimeType },
          { label: 'File Size', value: d.fileSize },
          { label: 'Version', value: d.version ? `v${d.version}` : undefined },
        ]} />
        <DetailSection title="Linked Entity" icon="ri-links-line" fields={[
          { label: 'Entity Type', value: d.linkedEntity },
          { label: 'Entity ID', value: d.linkedEntityId },
          { label: 'Uploaded By', value: d.uploadedBy },
          { label: 'Upload Date', value: formatDate(d.createdAtUtc) },
        ]} />
      </div>

      {d.tags && d.tags.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-3">Tags</h3>
          <div className="flex flex-wrap gap-2">
            {d.tags.map((tag: string) => (
              <span key={tag} className="inline-flex items-center rounded-full border border-gray-200 bg-gray-50 px-3 py-1 text-xs font-medium text-gray-600">{tag}</span>
            ))}
          </div>
        </div>
      )}

      {d.description && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Description</h3>
          <p className="text-sm text-gray-600">{d.description}</p>
        </div>
      )}

      {d.processingNotes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Processing Notes</h3>
          <p className="text-sm text-gray-600">{d.processingNotes}</p>
        </div>
      )}

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => { updateDocument(id, { status: confirmAction.status }); addToast({ type: 'success', title: confirmAction.label }); setConfirmAction(null); }}
          title={confirmAction.label} description={`${confirmAction.label} for ${d.fileName}?`} confirmLabel={confirmAction.label}
        />
      )}
    </div>
  );
}
