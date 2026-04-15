'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { ScheduleDto } from '@/lib/reports/reports.types';
import { reportsService } from '@/lib/reports/reports.service';
import { useSessionContext } from '@/providers/session-provider';

export function SchedulesListClient() {
  const router = useRouter();
  const { session } = useSessionContext();
  const [schedules, setSchedules] = useState<ScheduleDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const tenantId = session?.tenantId ?? '';

  const load = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await reportsService.getSchedules(tenantId);
      setSchedules(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load schedules');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => { load(); }, [load]);

  async function handleDeactivate(scheduleId: string) {
    try {
      await reportsService.deactivateSchedule(scheduleId);
      setSchedules((prev) => prev.filter((s) => s.scheduleId !== scheduleId));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to deactivate');
    }
  }

  async function handleRunNow(scheduleId: string) {
    try {
      await reportsService.runScheduleNow(scheduleId);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to trigger run');
    }
  }

  return (
    <div className="min-h-full bg-gray-50">
      <div className="max-w-6xl mx-auto px-6 py-8">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-bold text-gray-900">Report Schedules</h1>
            <p className="text-sm text-gray-500 mt-1">
              Manage automated report generation and delivery
            </p>
          </div>
          <button
            onClick={() => router.push('/insights/schedules/new')}
            className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 inline-flex items-center gap-2"
          >
            <i className="ri-add-line" />
            New Schedule
          </button>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg px-5 py-4 mb-6">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}

        {loading ? (
          <div className="flex items-center justify-center py-20">
            <i className="ri-loader-4-line animate-spin text-2xl text-gray-400" />
          </div>
        ) : schedules.length === 0 ? (
          <div className="bg-white border border-gray-200 rounded-lg px-6 py-12 text-center">
            <i className="ri-calendar-schedule-line text-4xl text-gray-300" />
            <p className="text-sm text-gray-500 mt-3">No schedules created yet.</p>
            <button
              onClick={() => router.push('/insights/schedules/new')}
              className="text-sm text-primary font-medium mt-3 hover:text-primary/80"
            >
              Create your first schedule
            </button>
          </div>
        ) : (
          <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b border-gray-200">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Name</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Frequency</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Format</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Delivery</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Next Run</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500">Status</th>
                  <th className="px-4 py-3 text-right text-xs font-semibold text-gray-500">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {schedules.map((s) => (
                  <tr key={s.scheduleId} className="hover:bg-gray-50/50">
                    <td className="px-4 py-3">
                      <button
                        onClick={() => router.push(`/insights/schedules/${s.scheduleId}`)}
                        className="text-sm font-medium text-gray-900 hover:text-primary"
                      >
                        {s.scheduleName}
                      </button>
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {reportsService.cronToHuman(s.cronExpression)}
                    </td>
                    <td className="px-4 py-3 text-gray-600">{s.exportFormat}</td>
                    <td className="px-4 py-3 text-gray-600">{s.deliveryMethod}</td>
                    <td className="px-4 py-3 text-gray-600">
                      {s.nextRunAtUtc
                        ? new Date(s.nextRunAtUtc).toLocaleString()
                        : '—'}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${
                        s.isActive
                          ? 'bg-green-100 text-green-700'
                          : 'bg-gray-100 text-gray-500'
                      }`}>
                        {s.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          onClick={() => handleRunNow(s.scheduleId)}
                          className="text-xs text-primary hover:text-primary/80"
                          title="Run now"
                        >
                          <i className="ri-play-line" />
                        </button>
                        <button
                          onClick={() => router.push(`/insights/schedules/${s.scheduleId}`)}
                          className="text-xs text-gray-500 hover:text-gray-700"
                          title="Edit"
                        >
                          <i className="ri-edit-line" />
                        </button>
                        <button
                          onClick={() => handleDeactivate(s.scheduleId)}
                          className="text-xs text-gray-500 hover:text-red-500"
                          title="Deactivate"
                        >
                          <i className="ri-delete-bin-line" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
