'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { LIEN_TYPE_LABELS } from '@/types/lien';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { ActivityTimeline } from '@/components/lien/activity-timeline';
import { ConfirmDialog, FormModal } from '@/components/lien/modal';

const LIEN_STEPS = ['Draft', 'Active', 'Negotiation', 'Sold', 'Closed'];
const STATUS_MAP: Record<string, string> = { Draft: 'Draft', Offered: 'Active', Sold: 'Sold', Withdrawn: 'Closed' };

export default function LienDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const getLienDetail = useLienStore((s) => s.getLienDetail);
  const updateLien = useLienStore((s) => s.updateLien);
  const addOffer = useLienStore((s) => s.addOffer);
  const updateOffer = useLienStore((s) => s.updateOffer);
  const addToast = useLienStore((s) => s.addToast);
  const addActivity = useLienStore((s) => s.addActivity);
  const role = useLienStore((s) => s.currentRole);

  const [showOfferModal, setShowOfferModal] = useState(false);
  const [offerAmount, setOfferAmount] = useState('');
  const [offerNotes, setOfferNotes] = useState('');
  const [confirmAction, setConfirmAction] = useState<{ type: string; offerId?: string } | null>(null);

  const d = getLienDetail(id) as any;
  if (!d) return <div className="p-10 text-center text-gray-400">Lien not found.</div>;

  const canEdit = canPerformAction(role, 'edit');
  const offers = d.offers || [];

  const handleSubmitOffer = () => {
    if (!offerAmount || isNaN(Number(offerAmount))) return;
    const offer = {
      id: `off-${Date.now()}`, lienId: id, buyerOrgId: 'o-buyer', buyerOrgName: 'My Organization',
      offerAmount: Number(offerAmount), notes: offerNotes || undefined, status: 'Pending' as const,
      createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    };
    addOffer(id, offer);
    addToast({ type: 'success', title: 'Offer Submitted', description: `$${Number(offerAmount).toLocaleString()} offer placed` });
    setOfferAmount('');
    setOfferNotes('');
    setShowOfferModal(false);
  };

  const handleOfferAction = (offerId: string, action: 'Accepted' | 'Rejected') => {
    updateOffer(id, offerId, { status: action });
    if (action === 'Accepted') {
      const offer = offers.find((o: any) => o.id === offerId);
      updateLien(id, { status: 'Sold', purchasePrice: offer?.offerAmount });
      addActivity({ type: 'lien_sold', description: `Lien ${d.lienNumber} sold for ${formatCurrency(offer?.offerAmount)}`, actor: 'Current User', timestamp: new Date().toISOString(), icon: 'ri-check-double-line', color: 'text-green-600' });
    }
    addToast({ type: action === 'Accepted' ? 'success' : 'info', title: `Offer ${action}` });
    setConfirmAction(null);
  };

  return (
    <div className="space-y-5">
      <DetailHeader title={d.lienNumber} subtitle={LIEN_TYPE_LABELS[d.lienType] ?? d.lienType}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/liens" backLabel="Back to Liens"
        meta={[
          { label: 'Case', value: d.caseRef ?? '\u2014' },
          { label: 'Jurisdiction', value: d.jurisdiction ?? '\u2014' },
          { label: 'Created', value: formatDate(d.createdAtUtc) },
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            {d.status === 'Draft' && <button onClick={() => { updateLien(id, { status: 'Offered', offerPrice: Math.round(d.originalAmount * 0.8) }); addToast({ type: 'success', title: 'Listed for Sale' }); }} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">List for Sale</button>}
            {d.status === 'Offered' && <button onClick={() => setShowOfferModal(true)} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Submit Offer</button>}
            {d.status === 'Offered' && <button onClick={() => { updateLien(id, { status: 'Withdrawn' }); addToast({ type: 'warning', title: 'Lien Withdrawn' }); }} className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Withdraw</button>}
          </div>
        ) : undefined}
      />

      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h3 className="text-sm font-semibold text-gray-800 mb-4">Lien Lifecycle</h3>
        <StatusProgress steps={LIEN_STEPS} currentStep={STATUS_MAP[d.status] || 'Draft'} />
      </div>

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
        <DetailSection title="Lien Summary" icon="ri-stack-line" fields={[
          { label: 'Lien Number', value: d.lienNumber },
          { label: 'Type', value: LIEN_TYPE_LABELS[d.lienType] ?? d.lienType },
          { label: 'Jurisdiction', value: d.jurisdiction },
          { label: 'Incident Date', value: d.incidentDate ? formatDate(d.incidentDate) : undefined },
          { label: 'Confidential', value: d.isConfidential ? 'Yes' : 'No' },
          { label: 'Case Reference', value: d.caseRef ? <Link href="/lien/cases" className="text-primary hover:underline">{d.caseRef}</Link> : undefined },
        ]} />
        <DetailSection title="Parties" icon="ri-group-line" fields={[
          { label: 'Subject', value: d.subjectParty ? `${d.subjectParty.firstName} ${d.subjectParty.lastName}` : d.isConfidential ? 'Confidential' : undefined },
          { label: 'Selling Organization', value: d.sellingOrg?.orgName },
          { label: 'Buying Organization', value: d.buyingOrg?.orgName },
          { label: 'Holding Organization', value: d.holdingOrg?.orgName },
        ]} />
      </div>

      {offers.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800">Offers ({offers.length})</h3>
          </div>
          <div className="divide-y divide-gray-100">
            {offers.map((offer: any) => (
              <div key={offer.id} className="px-5 py-3 flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-700 font-medium">{offer.buyerOrgName}</p>
                  <p className="text-xs text-gray-400">{offer.notes || 'No notes'} &middot; {formatDate(offer.createdAtUtc)}</p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-medium text-gray-900 tabular-nums">{formatCurrency(offer.offerAmount)}</span>
                  <StatusBadge status={offer.status} />
                  {canEdit && offer.status === 'Pending' && (
                    <div className="flex gap-1">
                      <button onClick={() => setConfirmAction({ type: 'accept', offerId: offer.id })} className="text-xs px-2 py-1 bg-green-100 text-green-700 rounded hover:bg-green-200">Accept</button>
                      <button onClick={() => setConfirmAction({ type: 'reject', offerId: offer.id })} className="text-xs px-2 py-1 bg-red-100 text-red-700 rounded hover:bg-red-200">Reject</button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {d.status === 'Offered' && offers.filter((o: any) => o.status === 'Pending').length > 0 && (
        <div className="flex items-center gap-2 p-3 bg-amber-50 border border-amber-200 rounded-lg">
          <i className="ri-alert-line text-amber-600" />
          <p className="text-xs text-amber-700"><span className="font-medium">Action Required:</span> This lien has {offers.filter((o: any) => o.status === 'Pending').length} pending offer(s) requiring review.</p>
        </div>
      )}

      <FormModal open={showOfferModal} onClose={() => setShowOfferModal(false)} onSubmit={handleSubmitOffer} title="Submit Offer" submitLabel="Submit Offer" submitDisabled={!offerAmount || isNaN(Number(offerAmount))}>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Offer Amount<span className="text-red-500 ml-0.5">*</span></label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">$</span>
              <input type="number" value={offerAmount} onChange={(e) => setOfferAmount(e.target.value)} placeholder="0.00"
                className="w-full border border-gray-200 rounded-lg pl-7 pr-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
            <textarea value={offerNotes} onChange={(e) => setOfferNotes(e.target.value)} placeholder="Optional notes..." rows={3}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary" />
          </div>
        </div>
      </FormModal>

      {confirmAction && confirmAction.offerId && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => handleOfferAction(confirmAction.offerId!, confirmAction.type === 'accept' ? 'Accepted' : 'Rejected')}
          title={confirmAction.type === 'accept' ? 'Accept Offer' : 'Reject Offer'}
          description={confirmAction.type === 'accept' ? 'Accept this offer and mark the lien as sold?' : 'Reject this offer? This cannot be undone.'}
          confirmLabel={confirmAction.type === 'accept' ? 'Accept' : 'Reject'}
          confirmVariant={confirmAction.type === 'reject' ? 'danger' : 'primary'}
        />
      )}
    </div>
  );
}
