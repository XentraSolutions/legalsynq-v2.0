'use client';

import { use } from 'react';
import Link from 'next/link';
import { MOCK_LIEN_DETAILS, MOCK_LIENS, MOCK_LIEN_HISTORY, formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { LIEN_TYPE_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { ActivityTimeline } from '@/components/lien/activity-timeline';

export default function LienDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const lien = MOCK_LIEN_DETAILS[id] ?? MOCK_LIENS.find((l) => l.id === id);
  if (!lien) return <div className="p-10 text-center text-gray-400">Lien not found.</div>;
  const d = { ...MOCK_LIENS.find((l) => l.id === id), ...lien } as typeof lien & { incidentDate?: string; description?: string; offerExpiresAtUtc?: string; offerNotes?: string; offers?: any[] };

  return (
    <div className="space-y-5">
      <DetailHeader
        title={d.lienNumber}
        subtitle={LIEN_TYPE_LABELS[d.lienType] ?? d.lienType}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/liens"
        backLabel="Back to Liens"
        meta={[
          { label: 'Case', value: d.caseRef ?? '\u2014' },
          { label: 'Jurisdiction', value: d.jurisdiction ?? '\u2014' },
          { label: 'Created', value: formatDate(d.createdAtUtc) },
        ]}
        actions={
          <div className="flex gap-2">
            <button className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Edit</button>
            {d.status === 'Draft' && <button className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">List for Sale</button>}
          </div>
        }
      />

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Original Amount</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{formatCurrency(d.originalAmount)}</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Offer Price</p>
          <p className="text-2xl font-bold text-blue-600 mt-1">{formatCurrency(d.offerPrice)}</p>
        </div>
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Purchase Price</p>
          <p className="text-2xl font-bold text-emerald-600 mt-1">{formatCurrency(d.purchasePrice)}</p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection
          title="Lien Summary"
          icon="ri-stack-line"
          fields={[
            { label: 'Lien Number', value: d.lienNumber },
            { label: 'Type', value: LIEN_TYPE_LABELS[d.lienType] ?? d.lienType },
            { label: 'Jurisdiction', value: d.jurisdiction },
            { label: 'Incident Date', value: d.incidentDate ? formatDate(d.incidentDate) : undefined },
            { label: 'Confidential', value: d.isConfidential ? 'Yes' : 'No' },
            { label: 'Case Reference', value: d.caseRef ? <Link href="/lien/cases" className="text-primary hover:underline">{d.caseRef}</Link> : undefined },
          ]}
        />
        <DetailSection
          title="Parties"
          icon="ri-group-line"
          fields={[
            { label: 'Subject', value: d.subjectParty ? `${d.subjectParty.firstName} ${d.subjectParty.lastName}` : d.isConfidential ? 'Confidential' : undefined },
            { label: 'Selling Organization', value: d.sellingOrg?.orgName },
            { label: 'Buying Organization', value: d.buyingOrg?.orgName },
            { label: 'Holding Organization', value: d.holdingOrg?.orgName },
          ]}
        />
      </div>

      {d.description && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Description</h3>
          <p className="text-sm text-gray-600">{d.description}</p>
        </div>
      )}

      {d.offers && d.offers.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800">Offers ({d.offers.length})</h3>
          </div>
          <div className="divide-y divide-gray-100">
            {d.offers.map((offer: any) => (
              <div key={offer.id} className="px-5 py-3 flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-700 font-medium">{offer.buyerOrgName}</p>
                  <p className="text-xs text-gray-400">{offer.notes || 'No notes'} &middot; {formatDate(offer.createdAtUtc)}</p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-medium text-gray-900 tabular-nums">{formatCurrency(offer.offerAmount)}</span>
                  <StatusBadge status={offer.status} />
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <ActivityTimeline
        events={MOCK_LIEN_HISTORY.map((h) => ({ action: h.label, timestamp: h.occurredAtUtc, actor: h.actorOrgName ?? 'System' }))}
        title="Status History"
      />
    </div>
  );
}
