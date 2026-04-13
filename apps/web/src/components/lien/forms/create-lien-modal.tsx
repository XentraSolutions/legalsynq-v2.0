'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { LIEN_TYPE_LABELS } from '@/types/lien';

interface CreateLienModalProps {
  open: boolean;
  onClose: () => void;
}

export function CreateLienModal({ open, onClose }: CreateLienModalProps) {
  const addLien = useLienStore((s) => s.addLien);
  const [form, setForm] = useState({ lienType: '', originalAmount: '', jurisdiction: '', caseRef: '', subjectFirst: '', subjectLast: '', isConfidential: false });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.lienType) e.lienType = 'Lien type is required';
    if (!form.originalAmount || isNaN(Number(form.originalAmount)) || Number(form.originalAmount) <= 0) e.originalAmount = 'Valid amount is required';
    if (!form.jurisdiction.trim()) e.jurisdiction = 'Jurisdiction is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = () => {
    if (!validate()) return;
    const id = `l-${Date.now()}`;
    const num = `LN-${new Date().getFullYear()}-${String(Math.floor(Math.random() * 9000 + 1000))}`;
    addLien({
      id, tenantId: 't1', lienNumber: num, lienType: form.lienType, status: 'Draft',
      originalAmount: Number(form.originalAmount), jurisdiction: form.jurisdiction,
      caseRef: form.caseRef || undefined, isConfidential: form.isConfidential,
      subjectParty: form.subjectFirst ? { firstName: form.subjectFirst, lastName: form.subjectLast } : undefined,
      sellingOrg: { orgId: 'o-self', orgName: 'Current Organization' },
      createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    });
    setForm({ lienType: '', originalAmount: '', jurisdiction: '', caseRef: '', subjectFirst: '', subjectLast: '', isConfidential: false });
    setErrors({});
    onClose();
  };

  const reset = () => { setForm({ lienType: '', originalAmount: '', jurisdiction: '', caseRef: '', subjectFirst: '', subjectLast: '', isConfidential: false }); setErrors({}); onClose(); };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Create Lien" subtitle="Add a new lien record" submitLabel="Create Lien" size="lg">
      <div className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Lien Type<span className="text-red-500 ml-0.5">*</span></label>
            <select value={form.lienType} onChange={(e) => setForm({ ...form, lienType: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${errors.lienType ? 'border-red-300' : 'border-gray-200'}`}>
              <option value="">Select type...</option>
              {Object.entries(LIEN_TYPE_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
            {errors.lienType && <p className="text-xs text-red-500 mt-1">{errors.lienType}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Original Amount<span className="text-red-500 ml-0.5">*</span></label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">$</span>
              <input type="number" value={form.originalAmount} onChange={(e) => setForm({ ...form, originalAmount: e.target.value })} placeholder="0.00"
                className={`w-full border rounded-lg pl-7 pr-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${errors.originalAmount ? 'border-red-300' : 'border-gray-200'}`} />
            </div>
            {errors.originalAmount && <p className="text-xs text-red-500 mt-1">{errors.originalAmount}</p>}
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Jurisdiction<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.jurisdiction} onChange={(e) => setForm({ ...form, jurisdiction: e.target.value })} placeholder="e.g. Nevada"
              className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${errors.jurisdiction ? 'border-red-300' : 'border-gray-200'}`} />
            {errors.jurisdiction && <p className="text-xs text-red-500 mt-1">{errors.jurisdiction}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Case Reference</label>
            <input type="text" value={form.caseRef} onChange={(e) => setForm({ ...form, caseRef: e.target.value })} placeholder="e.g. CASE-2024-0001"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Subject First Name</label>
            <input type="text" value={form.subjectFirst} onChange={(e) => setForm({ ...form, subjectFirst: e.target.value })} placeholder="First name"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Subject Last Name</label>
            <input type="text" value={form.subjectLast} onChange={(e) => setForm({ ...form, subjectLast: e.target.value })} placeholder="Last name"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div className="flex items-center gap-2">
          <input type="checkbox" id="confidential" checked={form.isConfidential} onChange={(e) => setForm({ ...form, isConfidential: e.target.checked })} className="rounded border-gray-300" />
          <label htmlFor="confidential" className="text-sm text-gray-600">Mark as confidential</label>
        </div>
      </div>
    </FormModal>
  );
}
