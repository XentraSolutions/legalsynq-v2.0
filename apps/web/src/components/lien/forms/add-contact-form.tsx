'use client';

import { useState } from 'react';
import { FormModal } from '@/components/lien/modal';
import { useLienStore } from '@/stores/lien-store';
import { CONTACT_TYPE_LABELS } from '@/types/lien';

interface AddContactFormProps {
  open: boolean;
  onClose: () => void;
}

export function AddContactForm({ open, onClose }: AddContactFormProps) {
  const addContact = useLienStore((s) => s.addContact);
  const [form, setForm] = useState({ name: '', contactType: '', organization: '', email: '', phone: '', city: '', state: '' });
  const [errors, setErrors] = useState<Record<string, string>>({});

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = 'Name is required';
    if (!form.contactType) e.contactType = 'Type is required';
    if (!form.organization.trim()) e.organization = 'Organization is required';
    if (!form.email.trim()) e.email = 'Email is required';
    else if (!/\S+@\S+\.\S+/.test(form.email)) e.email = 'Invalid email format';
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = () => {
    if (!validate()) return;
    addContact({
      id: `ct-${Date.now()}`, contactType: form.contactType, name: form.name,
      organization: form.organization, email: form.email, phone: form.phone,
      city: form.city, state: form.state, activeCases: 0, createdAtUtc: new Date().toISOString(),
    });
    setForm({ name: '', contactType: '', organization: '', email: '', phone: '', city: '', state: '' });
    setErrors({});
    onClose();
  };

  const reset = () => { setForm({ name: '', contactType: '', organization: '', email: '', phone: '', city: '', state: '' }); setErrors({}); onClose(); };

  return (
    <FormModal open={open} onClose={reset} onSubmit={handleSubmit} title="Add Contact" submitLabel="Add Contact">
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Name<span className="text-red-500 ml-0.5">*</span></label>
          <input type="text" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Full name"
            className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.name ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
          {errors.name && <p className="text-xs text-red-500 mt-1">{errors.name}</p>}
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Contact Type<span className="text-red-500 ml-0.5">*</span></label>
            <select value={form.contactType} onChange={(e) => setForm({ ...form, contactType: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.contactType ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}>
              <option value="">Select...</option>
              {Object.entries(CONTACT_TYPE_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
            {errors.contactType && <p className="text-xs text-red-500 mt-1">{errors.contactType}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Organization<span className="text-red-500 ml-0.5">*</span></label>
            <input type="text" value={form.organization} onChange={(e) => setForm({ ...form, organization: e.target.value })} placeholder="Organization"
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.organization ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.organization && <p className="text-xs text-red-500 mt-1">{errors.organization}</p>}
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email<span className="text-red-500 ml-0.5">*</span></label>
            <input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} placeholder="email@example.com"
              className={`w-full border rounded-lg px-3 py-2 text-sm ${errors.email ? 'border-red-300' : 'border-gray-200'} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`} />
            {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
            <input type="text" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} placeholder="(555) 555-0000"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">City</label>
            <input type="text" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} placeholder="City"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">State</label>
            <input type="text" value={form.state} onChange={(e) => setForm({ ...form, state: e.target.value })} placeholder="e.g. NV"
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
