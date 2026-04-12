'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useLienStore, canPerformAction } from '@/stores/lien-store';
import { formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';
import { StatusProgress } from '@/components/lien/status-progress';
import { ConfirmDialog } from '@/components/lien/modal';

const BOS_STEPS = ['Draft', 'Pending', 'Executed'];

export default function BillOfSaleDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const billsOfSale = useLienStore((s) => s.billsOfSale);
  const bosDetails = useLienStore((s) => s.bosDetails);
  const updateBos = useLienStore((s) => s.updateBos);
  const addToast = useLienStore((s) => s.addToast);
  const role = useLienStore((s) => s.currentRole);
  const [confirmAction, setConfirmAction] = useState<{ status: string; label: string } | null>(null);

  const summary = billsOfSale.find((b) => b.id === id);
  const detail = bosDetails[id];
  const bos = detail ? { ...summary, ...detail } : summary;
  if (!bos) return <div className="p-10 text-center text-gray-400">Bill of Sale not found.</div>;
  const d = bos as any;
  const canEdit = canPerformAction(role, 'edit');

  return (
    <div className="space-y-5">
      <DetailHeader title={d.bosNumber} subtitle={`Lien ${d.lienNumber}`}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/bill-of-sales" backLabel="Back to Bill of Sales"
        meta={[
          { label: 'Created', value: formatDate(d.createdAtUtc) },
          ...(d.executionDate ? [{ label: 'Executed', value: formatDate(d.executionDate) }] : []),
        ]}
        actions={canEdit ? (
          <div className="flex gap-2">
            {d.status === 'Draft' && <button onClick={() => setConfirmAction({ status: 'Pending', label: 'Submit for Execution' })} className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Submit for Execution</button>}
            {d.status === 'Pending' && <button onClick={() => setConfirmAction({ status: 'Executed', label: 'Execute' })} className="text-sm px-3 py-1.5 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700">Execute</button>}
            {(d.status === 'Draft' || d.status === 'Pending') && <button onClick={() => setConfirmAction({ status: 'Cancelled', label: 'Cancel' })} className="text-sm px-3 py-1.5 border border-red-200 text-red-600 rounded-lg hover:bg-red-50">Cancel</button>}
            <button onClick={() => addToast({ type: 'info', title: 'Print', description: 'Print functionality simulated' })} className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Print</button>
          </div>
        ) : undefined}
      />

      {d.status !== 'Cancelled' && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-4">BOS Workflow</h3>
          <StatusProgress steps={BOS_STEPS} currentStep={d.status === 'Cancelled' ? 'Draft' : d.status} />
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <p className="text-xs text-gray-400 font-medium">Sale Amount</p>
          <p className="text-2xl font-bold text-gray-900 mt-1">{formatCurrency(d.saleAmount)}</p>
        </div>
        {d.originalLienAmount && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <p className="text-xs text-gray-400 font-medium">Original Lien Amount</p>
            <p className="text-2xl font-bold text-gray-600 mt-1">{formatCurrency(d.originalLienAmount)}</p>
          </div>
        )}
        {d.discountPercent != null && (
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <p className="text-xs text-gray-400 font-medium">Discount</p>
            <p className="text-2xl font-bold text-amber-600 mt-1">{d.discountPercent.toFixed(1)}%</p>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
        <DetailSection title="Transaction Details" icon="ri-exchange-dollar-line" fields={[
          { label: 'BOS Number', value: d.bosNumber },
          { label: 'Lien Number', value: <Link href={`/lien/liens/${d.lienId}`} className="text-primary hover:underline">{d.lienNumber}</Link> },
          { label: 'Case Number', value: d.caseNumber },
          { label: 'Execution Date', value: d.executionDate ? formatDate(d.executionDate) : 'Pending' },
        ]} />
        <DetailSection title="Parties" icon="ri-group-line" fields={[
          { label: 'Seller', value: d.sellerOrg },
          { label: 'Seller Contact', value: d.sellerContact },
          { label: 'Buyer', value: d.buyerOrg },
          { label: 'Buyer Contact', value: d.buyerContact },
        ]} />
      </div>

      {d.terms && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Terms</h3>
          <p className="text-sm text-gray-600">{d.terms}</p>
        </div>
      )}
      {d.notes && (
        <div className="bg-white border border-gray-200 rounded-xl p-5">
          <h3 className="text-sm font-semibold text-gray-800 mb-2">Notes</h3>
          <p className="text-sm text-gray-600">{d.notes}</p>
        </div>
      )}

      {confirmAction && (
        <ConfirmDialog open onClose={() => setConfirmAction(null)}
          onConfirm={() => {
            updateBos(id, { status: confirmAction.status, ...(confirmAction.status === 'Executed' ? { executionDate: new Date().toISOString().split('T')[0] } : {}) });
            addToast({ type: confirmAction.status === 'Cancelled' ? 'warning' : 'success', title: confirmAction.label, description: `BOS ${d.bosNumber} ${confirmAction.status.toLowerCase()}` });
            setConfirmAction(null);
          }}
          title={confirmAction.label}
          description={`Are you sure you want to ${confirmAction.label.toLowerCase()} ${d.bosNumber}?`}
          confirmLabel={confirmAction.label}
          confirmVariant={confirmAction.status === 'Cancelled' ? 'danger' : 'primary'}
        />
      )}
    </div>
  );
}
