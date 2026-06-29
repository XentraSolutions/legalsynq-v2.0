'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { FormEvent, useState } from 'react';
import { PageHeader } from '@/components/lien/page-header';
import { lienSalesService } from '@/lib/liens/lien-sales.service';

export function NewLienSaleClient() {
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setSubmitting(true);
    setError(null);
    try {
      const portfolio = await lienSalesService.create({
        portfolioNumber: String(form.get('portfolioNumber') ?? '').trim(),
        name: String(form.get('name') ?? '').trim(),
        description: String(form.get('description') ?? '').trim(),
        internalNotes: String(form.get('internalNotes') ?? '').trim(),
        targetGrouping: String(form.get('targetGrouping') ?? '').trim(),
      });
      router.push(`/lien/sales/${portfolio.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create sale portfolio.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="New Sale Portfolio"
        breadcrumbs={[{ label: 'Lien Sales', href: '/lien/sales' }, { label: 'New' }]}
      />

      {error && <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{error}</div>}

      <form onSubmit={submit} className="space-y-4 rounded-lg border border-gray-200 bg-white p-4">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Field label="Portfolio Number" name="portfolioNumber" required placeholder="SALE-2026-001" />
          <Field label="Name" name="name" required placeholder="Q3 provider receivables" />
          <Field label="Target Grouping" name="targetGrouping" placeholder="Institutional pool" />
        </div>
        <Textarea label="Description" name="description" placeholder="Portfolio summary for internal review" />
        <Textarea label="Internal Notes" name="internalNotes" placeholder="Operational notes visible to seller users only" />
        <div className="flex items-center justify-end gap-2">
          <Link href="/lien/sales" className="rounded-md border border-gray-200 px-3 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50">Cancel</Link>
          <button disabled={submitting} className="rounded-md bg-primary px-3 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-60">
            {submitting ? 'Creating...' : 'Create Portfolio'}
          </button>
        </div>
      </form>
    </div>
  );
}

function Field({ label, name, required, placeholder }: { label: string; name: string; required?: boolean; placeholder?: string }) {
  return (
    <label className="block">
      <span className="text-xs font-medium text-gray-500">{label}</span>
      <input name={name} required={required} placeholder={placeholder} className="mt-1 min-h-10 w-full rounded-md border border-gray-200 px-3 text-sm outline-none focus:border-primary" />
    </label>
  );
}

function Textarea({ label, name, placeholder }: { label: string; name: string; placeholder?: string }) {
  return (
    <label className="block">
      <span className="text-xs font-medium text-gray-500">{label}</span>
      <textarea name={name} placeholder={placeholder} rows={4} className="mt-1 w-full rounded-md border border-gray-200 px-3 py-2 text-sm outline-none focus:border-primary" />
    </label>
  );
}
