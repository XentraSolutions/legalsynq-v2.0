import type { EffectivePermissionsResult, EffectivePermission } from '@/types/control-center';

interface EffectivePermissionsPanelProps {
  result:     EffectivePermissionsResult | null;
  fetchError: string | null;
}

function ProductBadge({ name }: { name: string }) {
  const colors: Record<string, string> = {
    'CareConnect': 'bg-teal-50 text-teal-700 border-teal-100',
    'SynqLien':    'bg-amber-50 text-amber-700 border-amber-100',
    'SynqFund':    'bg-violet-50 text-violet-700 border-violet-100',
  };
  const cls = colors[name] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold border ${cls}`}>
      {name}
    </span>
  );
}

function PermissionRow({ perm }: { perm: EffectivePermission }) {
  return (
    <div className="flex items-start gap-3 px-4 py-3 hover:bg-gray-50 transition-colors">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <code className="text-xs bg-gray-100 px-1.5 py-0.5 rounded font-mono text-gray-700">
            {perm.code}
          </code>
        </div>
        <p className="text-sm font-medium text-gray-900 mt-0.5">{perm.name}</p>
        {perm.description && (
          <p className="text-xs text-gray-500">{perm.description}</p>
        )}
      </div>
      {/* Source attribution */}
      <div className="shrink-0 flex flex-wrap gap-1 justify-end max-w-[160px]">
        {perm.sources.map((src, i) => (
          <span
            key={i}
            title={`Granted via ${src.type}: ${src.name}`}
            className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-medium bg-indigo-50 text-indigo-600 border border-indigo-100"
          >
            <svg className="h-2.5 w-2.5" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
              <path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6ZM12.735 14c.618 0 1.093-.561.872-1.139a6.002 6.002 0 0 0-11.215 0c-.22.578.254 1.139.872 1.139h9.47Z" />
            </svg>
            {src.name}
          </span>
        ))}
      </div>
    </div>
  );
}

export function EffectivePermissionsPanel({
  result,
  fetchError,
}: EffectivePermissionsPanelProps) {
  const byProduct = result?.items.reduce<Record<string, EffectivePermission[]>>((acc, p) => {
    if (!acc[p.productName]) acc[p.productName] = [];
    acc[p.productName].push(p);
    return acc;
  }, {}) ?? {};

  return (
    <div className="space-y-4">
      {/* Header */}
      <div>
        <h2 className="text-base font-semibold text-gray-900">Effective Permissions</h2>
        <p className="text-xs text-gray-500 mt-0.5">
          The union of all capabilities granted through this user&apos;s active roles.
        </p>
      </div>

      {/* Meta */}
      {result && !fetchError && (
        <div className="flex items-center gap-4 text-xs text-gray-400">
          <span>{result.totalCount} capability{result.totalCount !== 1 ? 's' : ''}</span>
          <span className="text-gray-200">·</span>
          <span>via {result.roleCount} role{result.roleCount !== 1 ? 's' : ''}</span>
        </div>
      )}

      {/* Error */}
      {fetchError && (
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {fetchError}
        </div>
      )}

      {/* Empty state */}
      {result && result.items.length === 0 && !fetchError && (
        <div className="bg-white border border-gray-200 rounded-lg p-8 text-center space-y-1">
          <p className="text-sm font-medium text-gray-700">No permissions</p>
          <p className="text-xs text-gray-400">
            This user has no active role assignments with capability permissions.
          </p>
        </div>
      )}

      {/* Permissions by product */}
      {result && result.items.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
          {Object.entries(byProduct).map(([product, perms], idx) => (
            <div key={product} className={idx > 0 ? 'border-t border-gray-100' : ''}>
              <div className="px-4 py-2 bg-gray-50 border-b border-gray-100 flex items-center gap-2">
                <ProductBadge name={product} />
                <span className="text-xs text-gray-400">
                  {perms.length} capability{perms.length !== 1 ? 's' : ''}
                </span>
              </div>
              <div className="divide-y divide-gray-50">
                {perms.map(perm => (
                  <PermissionRow key={perm.id} perm={perm} />
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Legend */}
      {result && result.items.length > 0 && (
        <p className="text-xs text-gray-400">
          Badges show which roles grant each capability.
        </p>
      )}
    </div>
  );
}
