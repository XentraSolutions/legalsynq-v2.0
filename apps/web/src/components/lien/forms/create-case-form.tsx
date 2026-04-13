'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';

interface CreateCaseFormProps {
  open: boolean;
  onClose: () => void;
}

export function CreateCaseForm({ open, onClose }: CreateCaseFormProps) {
  const addCase = useLienStore((s) => s.addCase);
  const [form, setForm] = useState({ clientName: '', lawFirm: '', medicalFacility: '', dateOfIncident: '', assignedTo: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.clientName.trim()) e.clientName = 'Client name is required';
    if (!form.lawFirm.trim()) e.lawFirm = 'Law firm is required';
    if (!form.medicalFacility.trim()) e.medicalFacility = 'Medical facility is required';
    if (!form.dateOfIncident) e.dateOfIncident = 'Date of incident is required';
    if (!form.assignedTo.trim()) e.assignedTo = 'Assignee is required';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = () => {
    if (!validate()) return;
    const id = `c-${Date.now()}`;
    const num = `CASE-${new Date().getFullYear()}-${String(Math.floor(Math.random() * 9000 + 1000))}`;
    addCase({
      id, caseNumber: num, status: 'PreDemand', clientName: form.clientName,
      lawFirm: form.lawFirm, medicalFacility: form.medicalFacility,
      dateOfIncident: form.dateOfIncident, totalLienAmount: 0, lienCount: 0,
      assignedTo: form.assignedTo, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    });
    setForm({ clientName: '', lawFirm: '', medicalFacility: '', dateOfIncident: '', assignedTo: '' });
    setErrors({});
    onClose();
  };

  const reset = () => { setForm({ clientName: '', lawFirm: '', medicalFacility: '', dateOfIncident: '', assignedTo: '' }); setErrors({}); onClose(); };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Create Case" subtitle="Add a new case to the system" submitLabel="Create Case" submitDisabled={!form.clientName || !form.lawFirm}>
      <div className="space-y-4">
        <Field label="Client Name" required value={form.clientName} onChange={(v) => setForm({ ...form, clientName: v })} error={errors.clientName} placeholder="Enter client name" />
        <Field label="Law Firm" required value={form.lawFirm} onChange={(v) => setForm({ ...form, lawFirm: v })} error={errors.lawFirm} placeholder="Enter law firm name" />
        <Field label="Medical Facility" required value={form.medicalFacility} onChange={(v) => setForm({ ...form, medicalFacility: v })} error={errors.medicalFacility} placeholder="Enter medical facility" />
        <Field label="Date of Incident" required value={form.dateOfIncident} onChange={(v) => setForm({ ...form, dateOfIncident: v })} error={errors.dateOfIncident} type="date" />
        <SelectField label="Assigned To" required value={form.assignedTo} onChange={(v) => setForm({ ...form, assignedTo: v })} error={errors.assignedTo} options={[
          { value: 'Sarah Chen', label: 'Sarah Chen' },
          { value: 'Michael Park', label: 'Michael Park' },
          { value: 'Lisa Wang', label: 'Lisa Wang' },
        ]} />
      </div>
    </FormModal>
  );
}

function Field({ label, value, onChange, error, placeholder, type = 'text', required }: { label: string; value: string; onChange: (v: string) => void; error?: string; placeholder?: string; type?: string; required?: boolean }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}{required && <span className="text-red-500 ml-0.5">*</span>}</label>
      <input type={type} value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder}
        className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? 'border-red-300' : 'border-gray-200'}`} />
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}

function SelectField({ label, value, onChange, error, options, required }: { label: string; value: string; onChange: (v: string) => void; error?: string; options: { value: string; label: string }[]; required?: boolean }) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">{label}{required && <span className="text-red-500 ml-0.5">*</span>}</label>
      <select value={value} onChange={(e) => onChange(e.target.value)}
        className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? 'border-red-300' : 'border-gray-200'}`}>
        <option value="">Select...</option>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}
