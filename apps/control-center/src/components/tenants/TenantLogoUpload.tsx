'use client';

import { useRef, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';

interface Props {
  tenantId:        string;
  logoDocumentId?: string;
}

/**
 * TenantLogoUpload — admin panel for uploading or removing a tenant's logo.
 *
 * Flow:
 *   Upload:  POST /api/tenants/[id]/logo  (multipart)
 *             → Documents service stores image
 *             → Identity stores the returned document ID
 *   Remove:  DELETE /api/tenants/[id]/logo
 *             → Identity clears the document ID
 */
export function TenantLogoUpload({ tenantId, logoDocumentId }: Props) {
  const router                      = useRouter();
  const fileInputRef                = useRef<HTMLInputElement>(null);
  const [pending, startTransition]  = useTransition();
  const [error, setError]           = useState<string | null>(null);
  const [preview, setPreview]       = useState<string | null>(null);
  const [currentDocId, setCurrentDocId] = useState<string | undefined>(logoDocumentId);

  const logoSrc = preview
    ?? (currentDocId ? `/api/tenants/${tenantId}/logo/content/${currentDocId}` : null);

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      setError('Please choose an image file (PNG, JPG, SVG, WEBP, etc.).');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('Image must be 5 MB or smaller.');
      return;
    }

    setError(null);
    setPreview(URL.createObjectURL(file));

    const form = new FormData();
    form.append('file', file);

    startTransition(async () => {
      try {
        const res = await fetch(`/api/tenants/${tenantId}/logo`, { method: 'POST', body: form });
        if (!res.ok) {
          const body = await res.json().catch(() => ({}));
          setError(body.error ?? 'Upload failed — please try again.');
          setPreview(null);
          return;
        }
        const { logoDocumentId: newDocId } = await res.json();
        setCurrentDocId(newDocId);
        setPreview(null);
        router.refresh();
      } catch {
        setError('Network error — please try again.');
        setPreview(null);
      }
    });

    e.target.value = '';
  }

  async function handleRemove() {
    setError(null);
    startTransition(async () => {
      try {
        const res = await fetch(`/api/tenants/${tenantId}/logo`, { method: 'DELETE' });
        if (!res.ok) {
          setError('Could not remove logo — please try again.');
          return;
        }
        setCurrentDocId(undefined);
        setPreview(null);
        router.refresh();
      } catch {
        setError('Network error — please try again.');
      }
    });
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
      <div className="px-5 py-3 border-b border-gray-100 bg-gray-50 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
          Tenant Logo
        </h2>
        {currentDocId && (
          <span className="text-[10px] text-green-600 font-medium uppercase tracking-wide flex items-center gap-1">
            <i className="ri-checkbox-circle-fill" />
            Logo set
          </span>
        )}
      </div>

      <div className="px-5 py-5 space-y-4">
        {/* Preview */}
        <div className="flex items-center gap-4">
          {logoSrc ? (
            <div className="w-24 h-14 rounded-lg border border-gray-200 bg-gray-50 flex items-center justify-center overflow-hidden shrink-0">
              <img
                src={logoSrc}
                alt="Tenant logo"
                className="max-w-full max-h-full object-contain"
              />
            </div>
          ) : (
            <div className="w-24 h-14 rounded-lg border-2 border-dashed border-gray-200 bg-gray-50 flex items-center justify-center shrink-0">
              <i className="ri-image-2-line text-gray-300 text-xl" />
            </div>
          )}

          <div className="space-y-1">
            <p className="text-xs text-gray-500">
              {currentDocId
                ? 'Shown in the tenant portal top bar.'
                : 'No logo set — portal uses the LegalSynq default.'}
            </p>
            <p className="text-[11px] text-gray-400">
              Recommended: PNG or SVG, transparent background, at least 200 × 60 px. Max 5 MB.
            </p>
          </div>
        </div>

        {error && (
          <p className="text-xs text-red-600 bg-red-50 border border-red-200 rounded-md px-3 py-2">
            {error}
          </p>
        )}

        {/* Actions */}
        <div className="flex gap-2 flex-wrap">
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={pending}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded-lg transition-colors"
          >
            {pending ? (
              <i className="ri-loader-4-line text-xs animate-spin" />
            ) : (
              <i className="ri-upload-2-line text-xs" />
            )}
            {currentDocId ? 'Replace logo' : 'Upload logo'}
          </button>

          {currentDocId && (
            <button
              type="button"
              onClick={handleRemove}
              disabled={pending}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-red-600 bg-white hover:bg-red-50 border border-red-200 disabled:opacity-50 rounded-lg transition-colors"
            >
              <i className="ri-delete-bin-line text-xs" />
              Remove logo
            </button>
          )}
        </div>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleFileChange}
      />
    </div>
  );
}
