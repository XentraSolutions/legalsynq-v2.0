'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';

interface CasesFilterProps {
  open: boolean;
  onClose: () => void;
  onApplyFilter?: () => void;
}

const INITIAL_FORM = {
  lawFirm: '',
  accidentType: '',
  caseManager: '',
  status: '',
};

export function CasesFilter({ open, onClose, onApplyFilter }: CasesFilterProps) {
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async () => {
    setSubmitting(true);
    setTimeout(() => {
    setSubmitting(false);
    onApplyFilter?.();
    }, 2000); // 2 seconds
  };

  const reset = () => {
    setForm({ ...INITIAL_FORM });
    onClose();
  };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Filter Cases" subtitle="Narrow down cases using filters to quickly find relevant results." submitLabel={submitting ? 'Filtering...' : 'Apply Filters'}>
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Field label="Law Firm"  value={form.lawFirm} onChange={(v) => setForm({ ...form, lawFirm: v })}/>
          <Field label="Accident Type"  value={form.accidentType} onChange={(v) => setForm({ ...form, accidentType: v })}/>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Case Manager"  value={form.caseManager} onChange={(v) => setForm({ ...form, caseManager: v })}/>
          <Field label="Status"  value={form.status} onChange={(v) => setForm({ ...form, status: v })}/>
        </div>
      </div>
    </FormModal>
  );
}

function Field({ label, value, onChange, required }:{ label: string; value: string; onChange: (v: string) => void; required?: boolean }) {
  return (
    <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">{label}{required && <span className="text-red-500 ml-0.5">*</span>}</label>
        <select className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white">
        <option value="">Select…</option>
        </select>
    </div>
  );
}
