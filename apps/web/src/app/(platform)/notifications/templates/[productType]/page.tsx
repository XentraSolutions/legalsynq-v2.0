import Link from 'next/link';
import { redirect } from 'next/navigation';
import { requireOrg } from '@/lib/auth-guards';
import {
  notificationsServerApi,
  PRODUCT_TYPES,
  PRODUCT_TYPE_LABELS,
  type GlobalTemplate,
  type ProductType,
} from '@/lib/notifications-server-api';

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
    });
  } catch { return iso; }
}

const CHANNEL_CLS: Record<string, string> = {
  email:   'bg-sky-50 text-sky-700 border-sky-200',
  sms:     'bg-violet-50 text-violet-700 border-violet-200',
  push:    'bg-orange-50 text-orange-700 border-orange-200',
  'in-app': 'bg-teal-50 text-teal-700 border-teal-200',
};

export default async function TemplateListPage({
  params,
}: {
  params: Promise<{ productType: string }>;
}) {
  const session = await requireOrg();
  const { productType } = await params;

  if (!PRODUCT_TYPES.includes(productType as ProductType)) {
    redirect('/notifications/templates');
  }

  const pt = productType as ProductType;
  let templates: GlobalTemplate[] = [];
  let fetchError: string | null = null;

  try {
    const res = await notificationsServerApi.globalTemplatesList(session.tenantId, {
      productType: pt,
      limit: 100,
    });
    templates = res.data;
  } catch (err) {
    fetchError = err instanceof Error ? err.message : 'Unable to load templates.';
  }

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <Link
              href="/notifications/templates"
              className="text-xs text-indigo-600 hover:text-indigo-500 font-medium flex items-center gap-1"
            >
              <i className="ri-arrow-left-line" /> All Products
            </Link>
          </div>
          <h1 className="text-2xl font-bold text-gray-900">
            {PRODUCT_TYPE_LABELS[pt]} Templates
          </h1>
          <p className="mt-1 text-sm text-gray-500">
            Notification templates for {PRODUCT_TYPE_LABELS[pt]}. These are managed by the platform &mdash;
            you can view and preview them with your branding.
          </p>
        </div>
        <span className="inline-flex items-center px-3 py-1.5 rounded-full text-xs font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
          {PRODUCT_TYPE_LABELS[pt]}
        </span>
      </div>

      {fetchError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <i className="ri-error-warning-line mr-1.5" />
          {fetchError}
        </div>
      ) : templates.length === 0 ? (
        <div className="bg-white rounded-lg border border-gray-200 py-16 text-center">
          <div className="mx-auto w-14 h-14 rounded-full bg-gray-100 flex items-center justify-center mb-4">
            <i className="ri-file-text-line text-2xl text-gray-400" />
          </div>
          <h2 className="text-base font-semibold text-gray-700 mb-1">No templates yet</h2>
          <p className="text-sm text-gray-400 max-w-sm mx-auto">
            There are no notification templates configured for {PRODUCT_TYPE_LABELS[pt]} yet.
            Templates are created and managed by the platform team.
          </p>
        </div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-100">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Name</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Key</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Channel</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Category</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Branded</th>
                <th className="px-5 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-gray-400">Updated</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {templates.map(t => {
                const channelCls = CHANNEL_CLS[t.channel.toLowerCase()] ?? 'bg-gray-50 text-gray-600 border-gray-200';
                return (
                  <tr key={t.id} className="hover:bg-gray-50 transition-colors">
                    <td className="px-5 py-3">
                      <Link
                        href={`/notifications/templates/${pt}/${t.id}`}
                        className="text-sm font-medium text-indigo-600 hover:text-indigo-500"
                      >
                        {t.name}
                      </Link>
                      {t.description && (
                        <p className="text-xs text-gray-400 mt-0.5 truncate max-w-[260px]">{t.description}</p>
                      )}
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-500 font-mono">{t.templateKey}</td>
                    <td className="px-5 py-3">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium capitalize border ${channelCls}`}>
                        {t.channel}
                      </span>
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-500">{t.category ?? '—'}</td>
                    <td className="px-5 py-3">
                      {t.isBrandable ? (
                        <span className="inline-flex items-center gap-1 text-xs text-emerald-600">
                          <i className="ri-check-line" /> Yes
                        </span>
                      ) : (
                        <span className="text-xs text-gray-400">No</span>
                      )}
                    </td>
                    <td className="px-5 py-3 text-xs text-gray-400 whitespace-nowrap">{fmtDate(t.updatedAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
