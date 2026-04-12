'use client';

import { use } from 'react';
import Link from 'next/link';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatDate } from '@/lib/lien-mock-data';
import { CONTACT_TYPE_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';

export default function ContactDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const contacts = useLienStore((s) => s.contacts);
  const contactDetails = useLienStore((s) => s.contactDetails);
  const cases = useLienStore((s) => s.cases);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);

  const summary = contacts.find((c) => c.id === id);
  const detail = contactDetails[id];
  const contact = detail ? { ...summary, ...detail } : summary;
  if (!contact) return <div className="p-10 text-center text-gray-400">Contact not found.</div>;
  const d = contact as any;
  const canEdit = canPerformAction(role, 'edit');

  const relatedCases = cases.filter((c) => c.lawFirm === d.organization || c.medicalFacility === d.organization || c.assignedTo === d.name).slice(0, 5);

  return (
    <div className="space-y-5">
      <DetailHeader title={d.name} subtitle={d.organization}
        badge={<span className="inline-flex items-center rounded-full border px-2.5 py-1 text-sm font-medium bg-gray-50 text-gray-600 border-gray-200">{CONTACT_TYPE_LABELS[d.contactType] ?? d.contactType}</span>}
        backHref="/lien/contacts" backLabel="Back to Contacts"
        meta={[
          ...(d.title ? [{ label: 'Title', value: d.title }] : []),
          { label: 'Active Cases', value: String(d.activeCases) },
          { label: 'Member Since', value: formatDate(d.createdAtUtc) },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            <button onClick={() => addToast({ type: 'info', title: 'Edit', description: 'Edit mode simulated' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Edit</button>
            <a href={`mailto:${d.email}`} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Send Email</a>
          </div>
        ) : undefined}
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Contact Information" icon="ri-contacts-book-line" fields={[
          { label: 'Email', value: d.email },
          { label: 'Phone', value: d.phone },
          { label: 'Fax', value: d.fax },
          { label: 'Website', value: d.website ? <a href={d.website} target="_blank" rel="noopener noreferrer" className="text-primary hover:underline">{d.website}</a> : undefined },
        ]} />
        <DetailSection title="Location" icon="ri-map-pin-line" fields={[
          { label: 'Address', value: d.address },
          { label: 'City', value: d.city },
          { label: 'State', value: d.state },
          { label: 'ZIP Code', value: d.zipCode },
        ]} />
      </div>

      {relatedCases.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800">Related Cases ({relatedCases.length})</h3>
            <Link href="/lien/cases" className="text-xs text-primary font-medium hover:underline">View All</Link>
          </div>
          <div className="divide-y divide-gray-100">
            {relatedCases.map((c) => (
              <Link key={c.id} href={`/lien/cases/${c.id}`} className="flex items-center justify-between px-5 py-3 hover:bg-gray-50 transition-colors">
                <div>
                  <span className="text-xs font-mono text-gray-500 mr-2">{c.caseNumber}</span>
                  <span className="text-sm text-gray-700">{c.clientName}</span>
                </div>
                <StatusBadge status={c.status} />
              </Link>
            ))}
          </div>
        </div>
      )}

      {d.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{d.notes}</p>
        </div>
      )}
    </div>
  );
}
