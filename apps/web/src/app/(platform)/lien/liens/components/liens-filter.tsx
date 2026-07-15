'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { DatePicker } from '@/components/ui/date-picker';

interface LiensFilterProps {
  open: boolean;
  onClose: () => void;
  onApplyFilter?: () => void;
}

const INITIAL_FORM = {
  lawFirm: '',
  medicalFacility: '',
  caseManager: '',
  status: '',
  purchaseDateFrom: '',
  purchaseDateTo: '',
  closedDateFrom: '',
  closedDateTo: '',
};

export function LiensFilter({ open, onClose, onApplyFilter }: LiensFilterProps) {
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
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Filter Liens" subtitle="Narrow down liens using filters to quickly find relevant results." submitLabel={submitting ? 'Filtering...' : 'Apply Filters'}>
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <Field label="Law Firm"  value={form.lawFirm} onChange={(v) => setForm({ ...form, lawFirm: v })}/>
          <Field label="Medical Facility"  value={form.medicalFacility} onChange={(v) => setForm({ ...form, medicalFacility: v })}/>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Case Manager"  value={form.caseManager} onChange={(v) => setForm({ ...form, caseManager: v })}/>
          <Field label="Status"  value={form.status} onChange={(v) => setForm({ ...form, status: v })}/>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Purchase Date From</label>
            <DatePicker
              value={form.purchaseDateFrom}
              onChange={(v) => setForm({ ...form, purchaseDateFrom: v })}
              disableFutureDates
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Purchase Date To</label>
            <DatePicker
              value={form.purchaseDateTo}
              onChange={(v) => setForm({ ...form, purchaseDateTo: v })}
              disableFutureDates
            />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Closed Date From</label>
            <DatePicker
              value={form.closedDateFrom}
              onChange={(v) => setForm({ ...form, closedDateFrom: v })}
              disableFutureDates
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Closed Date To</label>
            <DatePicker
              value={form.closedDateTo}
              onChange={(v) => setForm({ ...form, closedDateTo: v })}
              disableFutureDates
            />
          </div>
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