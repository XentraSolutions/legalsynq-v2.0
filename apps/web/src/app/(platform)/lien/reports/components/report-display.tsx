'use client';

import { StatusBadge } from "@/components/careconnect/status-badge";
import { KpiCard } from "@/components/lien/kpi-card";
import { CaseListItem } from "@/lib/cases";
import { useEffect, useState } from "react";


export default function ReportDisplay({ report, onBack, onEdit }: any) {
  const [loading, setLoading] = useState(true);
  const [cases, setCases] = useState<CaseListItem[]>([]);
  
  const viewBy = report.type; // 'cases' | 'liens'

  const metrics =
    viewBy === 'cases'
      ? [
          { label: 'Total Cases', value: 120 },
          { label: 'Open Cases', value: 45 },
          { label: 'Closed Cases', value: 75 },
          { label: 'Total Purchase Amount', value: '$85,000' },
          { label: 'Total Returned', value: '$12,000' },
          { label: 'Total Billing Amount', value: '$110,000' },
        ]
      : [
          { label: 'Total Liens', value: 340 },
          { label: 'Open Liens', value: 140 },
          { label: 'Closed Liens', value: 200 },
          { label: 'Total Purchase Amount', value: '$210,000' },
          { label: 'Total Returned', value: '$32,000' },
          { label: 'Total Billing Amount', value: '$275,000' },
        ];

    useEffect(() => {
      const timer = setTimeout(() => {
        setLoading(false);
      }, 2000); // 2 seconds

      return () => clearTimeout(timer);
    }, []);

  return (
    <div className="min-h-screen bg-gray-50 p-6 space-y-6">

      {/* HEADER */}
      <div className="flex justify-between items-center bg-white p-4 rounded-xl border border-gray-200">
        <div>
          <h2 className="text-lg font-semibold">{report.name}</h2>
          <p className="text-sm text-gray-500">
            {viewBy === 'cases' ? 'Cases Report' : 'Liens Report'}
          </p>
        </div>

        <div className="flex gap-2">
          <button className="px-3 py-2 bg-primary text-white rounded-lg text-sm hover:shadow-sm" onClick={onEdit}>
            Edit Template
          </button>

          {/* <button onClick={onBack} className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
            Back
          </button>
          <button className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
            Export CSV
          </button>
          <button className="px-3 py-2 bg-primary text-white rounded-lg text-sm">
            Save Template
          </button> */}
        </div>
      </div>

      {/* METRICS GRID */}
      <div className="grid grid-cols-1 md:grid-cols-6 gap-4">
        {metrics.map((m) => (
          <div key={m.label} className="border border-gray-200 rounded-xl p-5 hover:shadow-sm">
            <p className="text-xs text-gray-500">{m.label}</p>
            <p className="text-lg font-semibold">{m.value}</p>
          </div>
        ))}
      </div>

      {/* TABLE PLACEHOLDER */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="py-12 text-center">
            <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <p className="text-sm text-gray-400 mt-2">Loading cases...</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="bg-gray-50/80 border-b border-gray-100">
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Case ID</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Plaintiff Name</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Law Firm</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Case Manager</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Accident Type</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Date of Loss</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">DOB</th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {cases.map((c) => (
                    <tr
                      key={c.id}
                      className={`hover:bg-gray-50/80 transition-colors cursor-pointer`}
                    >
                      <td className="px-3 py-2.5">
                        {c.caseNumber}
                      </td>
                      <td className="px-3 py-2.5 text-sm text-gray-700 font-medium">{c.clientName}</td>
                      <td className="px-3 py-2.5 text-sm text-gray-600">{c.lawFirm || '—'}</td>
                      <td className="px-3 py-2.5 text-sm text-gray-600">{c.caseManager || '—'}</td>
                      <td className="px-3 py-2.5 text-sm text-gray-600">{c.accidentType || '—'}</td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 tabular-nums">{c.dateOfIncident || '—'}</td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 tabular-nums">{c.clientDob || '—'}</td>
                      <td className="px-3 py-2.5"><StatusBadge status={c.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {cases.length === 0 && !loading && (
              <div className="py-12 text-center">
                <i className="ri-folder-open-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">No data found.</p>
              </div>
            )}
          </>
        )}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-6 text-sm text-gray-500">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          {/* LEFT */}
          <button
            onClick={onBack}
            className="px-3 py-2 border border-gray-200 rounded-lg text-sm self-start hover:shadow-sm"
          >
            Go Back
          </button>
          {/* RIGHT */}
          <div className="flex flex-wrap gap-2 sm:gap-2 sm:flex-row sm:items-center sm:justify-end">
            <button className="px-3 py-2 border border-gray-200 text-red-500 rounded-lg text-sm hover:shadow-sm">
              Delete Template
            </button>

            <button className="px-3 py-2 border border-gray-200 text-blue-500 rounded-lg text-sm hover:shadow-sm">
              Export CSV
            </button>

            <button className="px-3 py-2 bg-primary text-white rounded-lg text-sm hover:shadow-sm">
              Save Template
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}