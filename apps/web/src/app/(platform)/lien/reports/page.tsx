'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { PageHeader } from '@/components/lien/page-header';
import CreateUpdateReport from './components/create-update-report';

const SAMPLE_REPORTS = [
  { id: '1', name: 'Case Summary Report', type: 'cases', createdAt: '2026-05-20', description: 'Cases overview report' },
  { id: '2', name: 'Lien Financial Report', type: 'liens', createdAt: '2026-05-22', description: 'Lien financial breakdown' },
];

export default function ReportsPage() {
  const router = useRouter();
  const [showCreate, setShowCreate] = useState(false);

  return (
    <div className="space-y-4">

      {/* HEADER */}
      <PageHeader
        title="Reports"
        subtitle={`${SAMPLE_REPORTS.length} saved reports`}
        actions={
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
          >
            <i className="ri-add-line text-base" />
            Create New Report
          </button>
        }
      />

      {/* LIST */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {SAMPLE_REPORTS.map((r) => (
          <div
            key={r.id}
            onClick={() => router.push(`/lien/reports/${r.id}`)}
            className="border border-gray-200 rounded-xl p-3 bg-white hover:bg-gray-50 cursor-pointer transition-colors"
          >
            <div className="text-sm font-medium text-gray-900">{r.name}</div>
            <div className="text-xs text-gray-500 mt-1">{r.description}</div>
            <div className="text-[11px] text-gray-400 mt-2">
              Created {r.createdAt}
            </div>
          </div>
        ))}
      </div>

      {/* CREATE MODAL */}
      {showCreate && (
        <CreateUpdateReport
          onClose={() => setShowCreate(false)}
          onSaved={(data: any) => {
            console.log('saved report:', data);
            setShowCreate(false);
          }}
        />
      )}

    </div>
  );
}