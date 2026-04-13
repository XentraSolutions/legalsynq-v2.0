'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { DOCUMENT_CATEGORY_LABELS } from '@/types/lien';

interface UploadDocumentFormProps {
  open: boolean;
  onClose: () => void;
  linkedEntity?: string;
  linkedEntityId?: string;
}

export function UploadDocumentForm({ open, onClose, linkedEntity, linkedEntityId }: UploadDocumentFormProps) {
  const addDocument = useLienStore((s) => s.addDocument);
  const [form, setForm] = useState({ fileName: '', category: '', linkedEntity: linkedEntity || '', linkedEntityId: linkedEntityId || '' });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [dragOver, setDragOver] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.fileName.trim()) e.fileName = 'File is required';
    if (!form.category) e.category = 'Category is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = () => {
    if (!validate()) return;
    const id = `doc-${Date.now()}`;
    const num = `DOC-${new Date().getFullYear()}-${String(Math.floor(Math.random() * 9000 + 1000))}`;
    addDocument({
      id, documentNumber: num, fileName: form.fileName, category: form.category,
      status: 'Pending', linkedEntity: form.linkedEntity || 'Unlinked',
      linkedEntityId: form.linkedEntityId || '', uploadedBy: 'Current User',
      fileSize: `${Math.floor(Math.random() * 5000 + 100)} KB`, createdAtUtc: new Date().toISOString(),
    });
    setForm({ fileName: '', category: '', linkedEntity: linkedEntity || '', linkedEntityId: linkedEntityId || '' });
    setErrors({});
    onClose();
  };

  const simulateFileSelect = () => {
    const names = ['treatment-records.pdf', 'billing-statement.pdf', 'demand-letter.docx', 'medical-report.pdf', 'lien-agreement.pdf'];
    setForm({ ...form, fileName: names[Math.floor(Math.random() * names.length)] });
  };

  const reset = () => { setForm({ fileName: '', category: '', linkedEntity: linkedEntity || '', linkedEntityId: linkedEntityId || '' }); setErrors({}); onClose(); };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Upload Document" submitLabel="Upload">
      <div className="space-y-4">
        <div
          className={`border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer ${dragOver ? 'border-primary bg-primary/5' : form.fileName ? 'border-green-300 bg-green-50' : 'border-gray-200'}`}
          onClick={simulateFileSelect}
          onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => { e.preventDefault(); setDragOver(false); simulateFileSelect(); }}
        >
          {form.fileName ? (
            <div className="flex items-center justify-center gap-2">
              <i className="ri-file-text-line text-2xl text-green-600" />
              <span className="text-sm font-medium text-gray-700">{form.fileName}</span>
              <button onClick={(e) => { e.stopPropagation(); setForm({ ...form, fileName: '' }); }} className="text-gray-400 hover:text-gray-600"><i className="ri-close-line" /></button>
            </div>
          ) : (
            <>
              <i className="ri-upload-cloud-2-line text-3xl text-gray-300 mb-2" />
              <p className="text-sm text-gray-500">Click or drag file to upload</p>
              <p className="text-xs text-gray-400 mt-1">PDF, DOCX, XLSX (max 10MB)</p>
            </>
          )}
        </div>
        {errors.fileName && <p className="text-xs text-red-500">{errors.fileName}</p>}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Category<span className="text-red-500 ml-0.5">*</span></label>
          <select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}
            className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.category ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}>
            <option value="">Select category...</option>
            {Object.entries(DOCUMENT_CATEGORY_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
          {errors.category && <p className="text-xs text-red-500 mt-1">{errors.category}</p>}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Link To</label>
            <select value={form.linkedEntity} onChange={(e) => setForm({ ...form, linkedEntity: e.target.value })}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary">
              <option value="">None</option>
              <option value="Case">Case</option>
              <option value="Lien">Lien</option>
              <option value="Bill of Sale">Bill of Sale</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Entity ID</label>
            <input type="text" value={form.linkedEntityId} onChange={(e) => setForm({ ...form, linkedEntityId: e.target.value })} placeholder="e.g. CASE-2024-0001"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
