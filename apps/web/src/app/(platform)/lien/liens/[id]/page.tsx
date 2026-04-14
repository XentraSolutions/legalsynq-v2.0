'use client';

import { use, useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useLienStore } from '@/stores/lien-store';
import { useRoleAccess } from '@/hooks/use-role-access';
import { ApiError } from '@/lib/api-client';
import { liensService, type LienDetail, type LienOfferItem } from '@/lib/liens';
import { casesService, type CaseDetail as CaseInfo } from '@/lib/cases';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { ConfirmDialog, FormModal } from '@/components/lien/modal';
import { EntityTimeline } from '@/components/lien/entity-timeline';
import { useProviderMode } from '@/hooks/use-provider-mode';

const SELL_LIEN_STEPS = ['Draft', 'Active', 'Negotiation', 'Sold', 'Closed'];
const MANAGE_LIEN_STEPS = ['Draft', 'Active', 'Closed'];
const STATUS_MAP: Record<string, string> = { Draft: 'Draft', Offered: 'Active', Sold: 'Sold', Withdrawn: 'Closed' };

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return '\u2014';
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(amount);
}

export default function LienDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const { isSellMode, isReady: modeReady } = useProviderMode();

  const [lien, setLien] = useState<LienDetail | null>(null);
  const [offers, setOffers] = useState<LienOfferItem[]>([]);
  const [linkedCase, setLinkedCase] = useState<CaseInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showOfferModal, setShowOfferModal] = useState(false);
  const [offerAmount, setOfferAmount] = useState('');
  const [offerNotes, setOfferNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [confirmAction, setConfirmAction] = useState<{ type: string; offerId?: string } | null>(null);

  const fetchLien = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const detailPromise = liensService.getLien(id);
      const offersPromise = isSellMode
        ? liensService.getLienOffers(id).catch(() => ({ items: [] as LienOfferItem[] }))
        : Promise.resolve({ items: [] as LienOfferItem[] });

      const [detail, offersResult] = await Promise.all([detailPromise, offersPromise]);
      setLien(detail);
      setOffers(offersResult.items);

      if (detail.caseId) {
        try {
          const caseDetail = await casesService.getCase(detail.caseId);
          setLinkedCase(caseDetail);
        } catch {
          setLinkedCase(null);
        }
      } else {
        setLinkedCase(null);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.isNotFound ? 'Lien not found.' : err.message);
      } else {
        setError('Failed to load lien details');
      }
    } finally {
      setLoading(false);
    }
  }, [id, isSellMode]);

  useEffect(() => {
    if (modeReady) fetchLien();
  }, [fetchLien, modeReady]);

  const canEdit = ra.can('lien:edit');

  if (loading) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p className="text-sm text-gray-400 mt-2">Loading lien details...</p>
      </div>
    );
  }

  if (error || !lien) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">{error || 'Lien not found.'}</p>
        <Link href="/lien/liens" className="text-sm text-primary hover:underline">Back to Liens</Link>
      </div>
    );
  }

  const d = lien;
  const pendingOffers = offers.filter((o) => o.status === 'Pending');

  const handleSubmitOffer = async () => {
    if (!offerAmount || isNaN(Number(offerAmount))) return;
    setSubmitting(true);
    try {
      await liensService.createOffer({
        lienId: id,
        offerAmount: Number(offerAmount),
        notes: offerNotes || undefined,
      });
      addToast({ type: 'success', title: 'Offer Submitted', description: `$${Number(offerAmount).toLocaleString()} offer placed` });
      setOfferAmount('');
      setOfferNotes('');
      setShowOfferModal(false);
      await fetchLien();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to submit offer';
      addToast({ type: 'error', title: 'Offer Failed', description: message });
    } finally {
      setSubmitting(false);
    }
  };

  const handleAcceptOffer = async (offerId: string) => {
    try {
      const result = await liensService.acceptOffer(offerId);
      addToast({ type: 'success', title: 'Offer Accepted', description: `Lien sold — Bill of Sale ${result.billOfSaleNumber} created` });
      setConfirmAction(null);
      await fetchLien();
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to accept offer';
      addToast({ type: 'error', title: 'Accept Failed', description: message });
      setConfirmAction(null);
    }
  };

  return (
    <div className="space-y-5">
      <DetailHeader title={d.lienNumber} subtitle={d.lienTypeLabel}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/liens" backLabel="Back to Liens"
        meta={[
          { label: 'Case', value: linkedCase ? linkedCase.caseNumber : d.caseId || '\u2014' },
          { label: 'Jurisdiction', value: d.jurisdiction || '\u2014' },
          { label: 'Created', value: d.createdAt },
        ]}
        actions={canEdit && isSellMode ? (
          <div className="flex gap-2">
            {d.status === 'Offered' && <button onClick={() => setShowOfferModal(true)} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Submit Offer</button>}
          </div>
        ) : undefined}
      />

      <div className="bg-white border border-gray-200 rounded-xl p-5">
        <h3 className="text-sm font-semibold text-gray-800 mb-4">Lien Lifecycle</h3>
        <StatusProgress steps={isSellMode ? SELL_LIEN_STEPS : MANAGE_LIEN_STEPS} currentStep={STATUS_MAP[d.status] || 'Draft'} />
      </div>

      <div className={`grid grid-cols-1 ${isSellMode ? 'sm:grid-cols-3' : 'sm:grid-cols-1'} gap-4`}>
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Original Amount</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{formatCurrency(d.originalAmount)}</p>
        </div>
        {isSellMode && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <p className="text-xs text-gray-400 font-medium">Offer Price</p>
            <p className="text-2xl font-bold text-blue-600 mt-1">{formatCurrency(d.offerPrice)}</p>
          </div>
        )}
        {isSellMode && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <p className="text-xs text-gray-400 font-medium">Purchase Price</p>
            <p className="text-2xl font-bold text-emerald-600 mt-1">{formatCurrency(d.purchasePrice)}</p>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Lien Summary" icon="ri-stack-line" fields={[
          { label: 'Lien Number', value: d.lienNumber },
          { label: 'Type', value: d.lienTypeLabel },
          { label: 'Jurisdiction', value: d.jurisdiction || undefined },
          { label: 'Incident Date', value: d.incidentDate || undefined },
          { label: 'Confidential', value: d.isConfidential ? 'Yes' : 'No' },
          { label: 'Case', value: linkedCase ? (
            <Link href={`/lien/cases/${d.caseId}`} className="text-primary hover:underline">{linkedCase.caseNumber} — {linkedCase.clientName}</Link>
          ) : d.caseId ? 'Linked (details unavailable)' : undefined },
        ]} />
        <DetailSection title="Subject Information" icon="ri-group-line" fields={[
          { label: 'Subject', value: d.isConfidential ? 'Confidential' : d.subjectName || undefined },
          { label: 'Description', value: d.description || undefined },
        ]} />
      </div>

      {isSellMode && offers.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl">
          <div className="px-5 py-4 border-b border-gray-100">
            <h3 className="text-sm font-semibold text-gray-800">Offers ({offers.length})</h3>
          </div>
          <div className="divide-y divide-gray-100">
            {offers.map((offer) => (
              <div key={offer.id} className="px-5 py-3 flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-700 font-medium">Offer from Org {offer.buyerOrgId.slice(0, 8)}...</p>
                  <p className="text-xs text-gray-400">{offer.notes || 'No notes'} &middot; {offer.offeredAt}</p>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-medium text-gray-900 tabular-nums">{formatCurrency(offer.offerAmount)}</span>
                  <StatusBadge status={offer.status} />
                  {canEdit && offer.status === 'Pending' && (
                    <div className="flex gap-1">
                      <button onClick={() => setConfirmAction({ type: 'accept', offerId: offer.id })} className="text-xs px-2 py-1 bg-green-100 text-green-700 rounded hover:bg-green-200">Accept</button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {isSellMode && pendingOffers.length > 0 && (
        <div className="flex items-center gap-2 p-3 bg-amber-50 border border-amber-200 rounded-lg">
          <i className="ri-alert-line text-amber-600" />
          <p className="text-xs text-amber-700"><span className="font-medium">Action Required:</span> This lien has {pendingOffers.length} pending offer(s) requiring review.</p>
        </div>
      )}

      <FormModal open={isSellMode && showOfferModal} onClose={() => setShowOfferModal(false)} onSubmit={handleSubmitOffer} title="Submit Offer" submitLabel={submitting ? 'Submitting...' : 'Submit Offer'} submitDisabled={!offerAmount || isNaN(Number(offerAmount)) || submitting}>
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

      <EntityTimeline entityType="Lien" entityId={id} />

      {confirmAction && confirmAction.offerId && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => handleAcceptOffer(confirmAction.offerId!)}
          title="Accept Offer"
          description="Accept this offer? This will mark the lien as sold and create a Bill of Sale. All other pending offers will be rejected."
          confirmLabel="Accept"
        />
      )}
    </div>
  );
}
