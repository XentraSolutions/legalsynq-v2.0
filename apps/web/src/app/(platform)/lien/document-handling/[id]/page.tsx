'use client';

import { use } from 'react';
import { MOCK_DOCUMENT_DETAILS, MOCK_DOCUMENTS, formatDate } from '@/lib/lien-mock-data';
import { DOCUMENT_CATEGORY_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';

export default function DocumentDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const doc = MOCK_DOCUMENT_DETAILS[id] ?? MOCK_DOCUMENTS.find((d) => d.id === id);
  if (!doc) return <div className="p-10 text-center text-gray-400">Document not found.</div>;
  const d = { ...MOCK_DOCUMENTS.find((dd) => dd.id === id), ...doc } as typeof doc & { description?: string; mimeType?: string; version?: number; tags?: string[]; processingNotes?: string };

  return (
    <div className="space-y-5">
      <DetailHeader
        title={d.documentNumber}
        subtitle={d.fileName}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/document-handling"
        backLabel="Back to Documents"
        meta={[
          { label: 'Category', value: DOCUMENT_CATEGORY_LABELS[d.category] ?? d.category },
          { label: 'Size', value: d.fileSize },
          { label: 'Uploaded', value: formatDate(d.createdAtUtc) },
        ]}
        actions={
          <div className="flex gap-2">
            <button className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">
              <i className="ri-download-2-line mr-1" />Download
            </button>
            <button className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">
              <i className="ri-share-line mr-1" />Share
            </button>
          </div>
        }
      />

      <div className="bg-white border border-gray-200 rounded-xl p-8">
        <div className="border-2 border-dashed border-gray-200 rounded-lg p-16 text-center">
          <i className="ri-file-text-line text-6xl text-gray-300 mb-4" />
          <p className="text-sm font-medium text-gray-500">Document Preview</p>
          <p className="text-xs text-gray-400 mt-1">{d.fileName}</p>
          <button className="mt-4 text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">Open Full Preview</button>
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
            {d.tags.map((tag) => (
              <span key={tag} className="inline-flex items-center rounded-full border border-gray-200 bg-gray-50 px-3 py-1 text-xs font-medium text-gray-600">
                {tag}
              </span>
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
    </div>
  );
}
