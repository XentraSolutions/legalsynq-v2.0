'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import { useToast } from '@/lib/toast-context';
import type { ReferralDetail } from '@/types/careconnect';

interface TreatmentType {
  id:   string;
  name: string;
}

interface ReferralTreatmentEditorProps {
  referral:   ReferralDetail;
  isReceiver: boolean;
}

const TERMINAL_STATUSES = ['Completed', 'Cancelled', 'Declined'];

export function ReferralTreatmentEditor({ referral, isReceiver }: ReferralTreatmentEditorProps) {
  const router = useRouter();
  const { show: showToast } = useToast();

  const [treatmentTypes,      setTreatmentTypes]      = useState<TreatmentType[]>([]);
  const [typesLoading,        setTypesLoading]        = useState(false);
  const [editing,             setEditing]             = useState(false);
  const [selected,            setSelected]            = useState<string>(referral.treatmentTypeId ?? '');
  const [loading,             setLoading]             = useState(false);
  const [error,               setError]               = useState<string | null>(null);

  const isTerminal = TERMINAL_STATUSES.includes(referral.status);

  useEffect(() => {
    if (!isReceiver || isTerminal) return;
    setTypesLoading(true);
    fetch('/api/careconnect/api/treatment-types')
      .then(r => r.ok ? r.json() : null)
      .then((data: TreatmentType[] | null) => { if (data) setTreatmentTypes(data); })
      .catch(() => {})
      .finally(() => setTypesLoading(false));
  }, [isReceiver, isTerminal]);

  if (!isReceiver || isTerminal) return null;

  async function handleSave() {
    setLoading(true);
    setError(null);
    try {
      // Guid.Empty string signals "clear" to the backend
      const treatmentTypeId = selected === '' ? '00000000-0000-0000-0000-000000000000' : selected;
      await careConnectApi.referrals.update(referral.id, {
        requestedService: referral.requestedService,
        urgency:          referral.urgency,
        status:           referral.status,
        notes:            referral.notes,
        treatmentTypeId,
      });
      setEditing(false);
      showToast('Treatment type updated.', 'success');
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        setError(err.message);
        showToast(err.message, 'error');
      } else {
        setError('Failed to update. Please try again.');
        showToast('Failed to update treatment type.', 'error');
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
        Type of Treatment
      </h3>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {!editing ? (
        <div className="flex items-center justify-between gap-3">
          <span className="text-sm text-gray-900">
            {referral.treatmentTypeName ?? <span className="text-gray-400 italic">Not set</span>}
          </span>
          <button
            onClick={() => { setSelected(referral.treatmentTypeId ?? ''); setEditing(true); }}
            disabled={typesLoading}
            className="text-xs text-primary hover:underline disabled:opacity-50 disabled:cursor-wait"
          >
            {typesLoading ? 'Loading…' : (referral.treatmentTypeName ? 'Change' : 'Set treatment type')}
          </button>
        </div>
      ) : (
        <div className="space-y-3">
          <select
            value={selected}
            onChange={e => setSelected(e.target.value)}
            disabled={loading}
            className="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary bg-white disabled:opacity-60"
          >
            <option value="">— None / Clear —</option>
            {treatmentTypes.map(t => (
              <option key={t.id} value={t.id}>{t.name}</option>
            ))}
          </select>
          <div className="flex items-center gap-3">
            <button
              onClick={handleSave}
              disabled={loading}
              className="bg-primary text-white text-sm font-medium px-4 py-1.5 rounded-md hover:opacity-90 disabled:opacity-60 transition-opacity"
            >
              {loading ? 'Saving…' : 'Save'}
            </button>
            <button
              onClick={() => { setEditing(false); setError(null); }}
              disabled={loading}
              className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
