'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import ReportDisplay from '../components/report-display';
import CreateUpdateReport from '../components/create-update-report';

const SAMPLE_REPORTS: any[] = [
  {
    id: '1',
    reportName: 'Lien Summary Report',
    reportDescription: 'Overview of all lien activities',
    createdAt: '2026-05-20',
    config: {
      columns: ['Plaintiff Name', 'Law Firm', 'Total Liens'],
    },
  },
  {
    id: '2',
    reportName: 'Billing Performance Report',
    reportDescription: 'Financial breakdown of billing',
    createdAt: '2026-05-18',
    config: {
      columns: ['Attorney', 'Total Billing Amount', 'Total Returned'],
    },
  },
];

export default function ReportDetailsPage() {
  const { id } = useParams();
  const router = useRouter();

  const [report, setReport] = useState<any | null>(null);
  const [loading, setLoading] = useState(true);
  const [editMode, setEditMode] = useState(false);

  useEffect(() => {
    setLoading(true);

    // simulate fetch
    const found = SAMPLE_REPORTS.find((r) => r.id === id);

    setTimeout(() => {
      setReport(found || null);
      setLoading(false);
    }, 300);
  }, [id]);

  if (loading) {
    return (
      <div className="p-6 text-sm text-gray-500">
        Loading report...
      </div>
    );
  }

  if (!report) {
    return (
      <div className="p-6 space-y-2">
        <p className="text-sm text-gray-500">Report not found</p>
        <button
          onClick={() => router.push('/lien/reports')}
          className="text-primary text-sm"
        >
          Back to Reports
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
        <ReportDisplay
        report={report}
        onBack={() => router.push('/lien/reports')}
        onEdit={() => setEditMode(true)}
        />
        {editMode ? (
        <CreateUpdateReport
            mode="edit"
            initialData={report}
            onClose={() => setEditMode(false)}
        />
        ) : ( "" )}
    </div>
  );
}