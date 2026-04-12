'use client';

import { use } from 'react';
import Link from 'next/link';
import { MOCK_BOS_DETAILS, MOCK_BILLS_OF_SALE, formatCurrency, formatDate } from '@/lib/lien-mock-data';
import { DetailHeader, DetailSection } from '@/components/lien/detail-section';
import { StatusBadge } from '@/components/lien/status-badge';

export default function BillOfSaleDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const bos = MOCK_BOS_DETAILS[id] ?? MOCK_BILLS_OF_SALE.find((b) => b.id === id);
  if (!bos) return <div className="p-10 text-center text-gray-400">Bill of Sale not found.</div>;
  const d = { ...MOCK_BILLS_OF_SALE.find((b) => b.id === id), ...bos } as typeof bos & { originalLienAmount?: number; discountPercent?: number; sellerContact?: string; buyerContact?: string; terms?: string; notes?: string };

  return (
    <div className="space-y-5">
      <DetailHeader
        title={d.bosNumber}
        subtitle={`Lien ${d.lienNumber}`}
        badge={<StatusBadge status={d.status} size="md" />}
        backHref="/lien/bill-of-sales"
        backLabel="Back to Bill of Sales"
        meta={[
          { label: 'Created', value: formatDate(d.createdAtUtc) },
          ...(d.executionDate ? [{ label: 'Executed', value: formatDate(d.executionDate) }] : []),
        ]}
        actions={
          <div className="flex gap-2">
            {d.status === 'Draft' && <button className="text-sm px-3 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90">Submit for Execution</button>}
            {d.status === 'Pending' && <button className="text-sm px-3 py-1.5 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700">Execute</button>}
            <button className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600">Print</button>
          </div>
        }
      />

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
    </div>
  );
}
