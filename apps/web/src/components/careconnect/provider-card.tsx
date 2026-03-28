import Link from 'next/link';
import type { ProviderSummary } from '@/types/careconnect';

interface ProviderCardProps {
  provider: ProviderSummary;
}

export function ProviderCard({ provider }: ProviderCardProps) {
  const isAccepting = provider.acceptingReferrals;

  return (
    <Link
      href={`/careconnect/providers/${provider.id}`}
      className="block bg-white border border-gray-200 rounded-lg p-4 hover:border-primary hover:shadow-sm transition-all"
    >
      <div className="flex items-start justify-between gap-4">
        {/* Name + org + location */}
        <div className="min-w-0 flex-1">
          <p className="font-medium text-gray-900 truncate">{provider.displayLabel}</p>
          {provider.organizationName && provider.organizationName !== provider.name && (
            <p className="text-sm text-gray-500 truncate">{provider.organizationName}</p>
          )}
          <p className="text-sm text-gray-500 mt-0.5">{provider.markerSubtitle}</p>
        </div>

        {/* Accepting referrals badge */}
        <span
          className={`shrink-0 inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium border ${
            isAccepting
              ? 'bg-green-50 text-green-700 border-green-200'
              : 'bg-gray-50 text-gray-500 border-gray-200'
          }`}
        >
          {isAccepting ? 'Accepting' : 'Not accepting'}
        </span>
      </div>

      {/* Categories */}
      {provider.categories.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-1.5">
          {provider.categories.slice(0, 4).map(cat => (
            <span
              key={cat}
              className="inline-flex items-center rounded px-1.5 py-0.5 text-xs bg-gray-100 text-gray-600"
            >
              {cat}
            </span>
          ))}
          {provider.categories.length > 4 && (
            <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs text-gray-400">
              +{provider.categories.length - 4} more
            </span>
          )}
        </div>
      )}

      {/* Contact */}
      <div className="mt-3 flex items-center gap-4 text-xs text-gray-400">
        {provider.phone && <span>{provider.phone}</span>}
        {provider.email && <span className="truncate">{provider.email}</span>}
      </div>
    </Link>
  );
}
