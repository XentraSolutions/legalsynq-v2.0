'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
interface TreatmentType {
  id:   string;
  name: string;
}

interface ReferralTreatmentFieldProps {
  referralId:         string;
  treatmentTypeId?:   string;
  treatmentTypeName?: string;
  urgency:            string;
  isReceiver:         boolean;
  status:             string;
}

const TERMINAL_STATUSES = ['Completed', 'Cancelled', 'Declined'];

export function ReferralTreatmentField({
  referralId,
  treatmentTypeId,
  treatmentTypeName,
  urgency,
  isReceiver,
  status,
}: ReferralTreatmentFieldProps) {
  const router = useRouter();

  const [treatmentTypes, setTreatmentTypes] = useState<TreatmentType[]>([]);
  const [typesLoading,   setTypesLoading]   = useState(false);
  const [editing,        setEditing]        = useState(false);
  const [selected,       setSelected]       = useState<string>(treatmentTypeId ?? '');
  const [saving,         setSaving]         = useState(false);
  const [error,          setError]          = useState<string | null>(null);

  const isTerminal = TERMINAL_STATUSES.includes(status);
  const canEdit    = isReceiver && !isTerminal;

  useEffect(() => {
    if (!canEdit) return;
    setTypesLoading(true);
    fetch('/api/careconnect/api/treatment-types')
      .then(r => r.ok ? r.json() : null)
      .then((data: TreatmentType[] | null) => { if (data) setTreatmentTypes(data); })
      .catch(() => {})
      .finally(() => setTypesLoading(false));
  }, [canEdit]);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      const treatmentTypeIdToSend = selected === '' ? '00000000-0000-0000-0000-000000000000' : selected;
      await careConnectApi.referrals.update(referralId, {
        urgency,
        treatmentTypeId: treatmentTypeIdToSend,
        // status intentionally omitted — treatment-type-only update
      });
      setEditing(false);
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        setError(err.message);
      } else {
        setError('Failed to update. Please try again.');
      }
    } finally {
      setSaving(false);
    }
  }

  if (!canEdit) {
    return (
      <div>
        <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">Type of treatment</dt>
        <dd className="mt-1 text-sm text-gray-900">{treatmentTypeName ?? '—'}</dd>
      </div>
    );
  }

  return (
    <div>
      <dt className="text-xs font-medium text-gray-500 uppercase tracking-wide">Type of treatment</dt>

      {error && (
        <p className="mt-1 text-xs text-red-600">{error}</p>
      )}

      {!editing ? (
        <dd className="mt-1 flex items-center gap-2">
          <span className="text-sm text-gray-900">
            {treatmentTypeName ?? <span className="text-gray-400 italic">Not set</span>}
          </span>
          <button
            onClick={() => { setSelected(treatmentTypeId ?? ''); setEditing(true); }}
            disabled={typesLoading}
            className="text-xs text-primary hover:underline disabled:opacity-50 disabled:cursor-wait shrink-0"
          >
            {typesLoading ? 'Loading…' : (treatmentTypeName ? 'Change' : 'Set')}
          </button>
        </dd>
      ) : (
        <dd className="mt-1 space-y-2">
          <select
            value={selected}
            onChange={e => setSelected(e.target.value)}
            disabled={saving}
            className="w-full border border-gray-300 rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white disabled:opacity-60"
          >
            <option value="">— None / Clear —</option>
            {treatmentTypes.map(t => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
          <div className="flex items-center gap-3">
            <button
              onClick={handleSave}
              disabled={saving}
              className="bg-primary text-white text-xs font-medium px-3 py-1 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
            >
              {saving ? 'Saving…' : 'Save'}
            </button>
            <button
              onClick={() => { setEditing(false); setError(null); }}
              disabled={saving}
              className="text-xs text-gray-500 hover:text-gray-800 transition-colors"
            >
              Cancel
            </button>
          </div>
        </dd>
      )}
    </div>
  );
}
