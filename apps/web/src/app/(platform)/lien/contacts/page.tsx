'use client';

import { useState } from 'react';
import Link from 'next/link';
import { PageHeader } from '@/components/lien/page-header';
import { FilterToolbar } from '@/components/lien/filter-toolbar';
import { ActionMenu } from '@/components/lien/action-menu';
import { SideDrawer } from '@/components/lien/side-drawer';
import { AddContactForm } from '@/components/lien/forms/add-contact-form';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { CONTACT_TYPE_LABELS } from '@/types/lien';

export default function ContactsPage() {
  const contacts = useLienStore((s) => s.contacts);
  const role = useLienStore((s) => s.currentRole);
  const addToast = useLienStore((s) => s.addToast);
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [previewId, setPreviewId] = useState<string | null>(null);

  const filtered = contacts.filter((c) => {
    if (search && !c.name.toLowerCase().includes(search.toLowerCase()) && !c.organization.toLowerCase().includes(search.toLowerCase()) && !c.email.toLowerCase().includes(search.toLowerCase())) return false;
    if (typeFilter && c.contactType !== typeFilter) return false;
    return true;
  });

  const previewContact = previewId ? contacts.find((c) => c.id === previewId) : null;

  return (
    <div className="space-y-5">
      <PageHeader title="Contacts" subtitle={`${filtered.length} contacts`}
        actions={canPerformAction(role, 'create') ? (
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors">
            <i className="ri-add-line text-base" />Add Contact
          </button>
        ) : undefined}
      />
      <FilterToolbar searchPlaceholder="Search contacts by name, org, or email..." onSearch={setSearch} filters={[
        { label: 'All Types', value: typeFilter, onChange: setTypeFilter, options: Object.entries(CONTACT_TYPE_LABELS).map(([v, l]) => ({ value: v, label: l })) },
      ]} />
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-100">
            <thead><tr className="bg-gray-50">
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Type</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Organization</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Email</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Phone</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Location</th>
              <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">Cases</th>
              <th className="px-4 py-3" />
            </tr></thead>
            <tbody className="divide-y divide-gray-100">
              {filtered.map((c) => (
                <tr key={c.id} className="hover:bg-gray-50 transition-colors cursor-pointer" onClick={() => setPreviewId(c.id)}>
                  <td className="px-4 py-3"><Link href={`/lien/contacts/${c.id}`} onClick={(e) => e.stopPropagation()} className="text-sm font-medium text-gray-700 hover:text-primary">{c.name}</Link></td>
                  <td className="px-4 py-3"><span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium bg-gray-50 text-gray-600 border-gray-200">{CONTACT_TYPE_LABELS[c.contactType] ?? c.contactType}</span></td>
                  <td className="px-4 py-3 text-sm text-gray-600">{c.organization}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{c.email}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{c.phone}</td>
                  <td className="px-4 py-3 text-sm text-gray-500">{c.city}, {c.state}</td>
                  <td className="px-4 py-3 text-sm text-gray-700 tabular-nums">{c.activeCases}</td>
                  <td className="px-4 py-3 text-right" onClick={(e) => e.stopPropagation()}>
                    <ActionMenu items={[
                      { label: 'View Details', icon: 'ri-eye-line', onClick: () => {} },
                      { label: 'Send Email', icon: 'ri-mail-line', onClick: () => addToast({ type: 'info', title: 'Email', description: `Opening email to ${c.email}` }) },
                    ]} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length === 0 && <div className="p-10 text-center text-sm text-gray-400">No contacts found.</div>}
      </div>

      <AddContactForm open={showCreate} onClose={() => setShowCreate(false)} />

      <SideDrawer open={!!previewContact} onClose={() => setPreviewId(null)} title={previewContact?.name || ''} subtitle={previewContact?.organization}>
        {previewContact && (
          <div className="space-y-4">
            <span className="inline-flex items-center rounded-full border px-2.5 py-1 text-sm font-medium bg-gray-50 text-gray-600 border-gray-200">{CONTACT_TYPE_LABELS[previewContact.contactType] ?? previewContact.contactType}</span>
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><p className="text-xs text-gray-400">Email</p><p className="text-gray-700">{previewContact.email}</p></div>
              <div><p className="text-xs text-gray-400">Phone</p><p className="text-gray-700">{previewContact.phone}</p></div>
              <div><p className="text-xs text-gray-400">Location</p><p className="text-gray-700">{previewContact.city}, {previewContact.state}</p></div>
              <div><p className="text-xs text-gray-400">Active Cases</p><p className="text-gray-700">{previewContact.activeCases}</p></div>
            </div>
            <Link href={`/lien/contacts/${previewContact.id}`} className="block text-center text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90">View Full Details</Link>
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
