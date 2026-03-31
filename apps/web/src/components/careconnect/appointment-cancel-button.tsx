'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import type { AppointmentDetail } from '@/types/careconnect';

interface AppointmentCancelButtonProps {
  appointment: AppointmentDetail;
}

/**
 * Cancel button for appointment detail page.
 *
 * Calls POST /api/appointments/{id}/cancel.
 * Shows a confirmation inline with an optional notes field.
 * Only rendered for non-terminal statuses (Scheduled / Confirmed).
 */
export function AppointmentCancelButton({ appointment }: AppointmentCancelButtonProps) {
  const router = useRouter();

  const isTerminal = ['Cancelled', 'Completed', 'NoShow'].includes(appointment.status);
  const [confirming, setConfirming] = useState(false);
  const [notes,      setNotes]      = useState('');
  const [loading,    setLoading]    = useState(false);
  const [error,      setError]      = useState<string | null>(null);

  if (isTerminal) return null;

  async function handleCancel() {
    setLoading(true);
    setError(null);
    try {
      await careConnectApi.appointments.cancel(appointment.id, { notes: notes.trim() || undefined });
      router.refresh();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) { router.push('/login'); return; }
        if (err.isForbidden)    { setError('You do not have permission to cancel this appointment.'); return; }
        setError(err.message);
      } else {
        setError('Failed to cancel the appointment. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-3">
      <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Actions</h3>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      {!confirming ? (
        <button
          onClick={() => setConfirming(true)}
          className="border border-red-300 text-red-600 text-sm font-medium px-4 py-2 rounded-md hover:bg-red-50 transition-colors"
        >
          Cancel Appointment
        </button>
      ) : (
        <div className="space-y-3 border border-red-100 rounded-md p-3 bg-red-50">
          <p className="text-sm font-medium text-red-800">
            Are you sure you want to cancel this appointment?
          </p>
          <div>
            <label className="block text-xs font-medium text-red-700 mb-1">
              Reason (optional)
            </label>
            <textarea
              value={notes}
              onChange={e => setNotes(e.target.value)}
              rows={2}
              placeholder="Provide a reason for cancellation…"
              className="w-full border border-red-200 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-red-400 resize-none bg-white"
            />
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={handleCancel}
              disabled={loading}
              className="bg-red-600 text-white text-sm font-medium px-4 py-1.5 rounded-md hover:bg-red-700 disabled:opacity-60 transition-colors"
            >
              {loading ? 'Cancelling…' : 'Yes, Cancel'}
            </button>
            <button
              onClick={() => { setConfirming(false); setNotes(''); setError(null); }}
              disabled={loading}
              className="text-sm text-gray-500 hover:text-gray-800 transition-colors"
            >
              Keep Appointment
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
