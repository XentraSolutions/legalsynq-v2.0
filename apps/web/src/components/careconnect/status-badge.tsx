import type { ReferralStatusValue } from '@/types/careconnect';

interface StatusBadgeProps {
  status: string;
  size?: 'sm' | 'md';
}

const STATUS_STYLES: Record<string, string> = {
  Pending:   'bg-yellow-50  text-yellow-700  border-yellow-200',
  Accepted:  'bg-blue-50    text-blue-700    border-blue-200',
  Declined:  'bg-red-50     text-red-700     border-red-200',
  Completed: 'bg-green-50   text-green-700   border-green-200',
  Cancelled: 'bg-gray-50    text-gray-600    border-gray-200',
};

const URGENCY_STYLES: Record<string, string> = {
  Routine:   'bg-gray-50    text-gray-600    border-gray-200',
  Urgent:    'bg-orange-50  text-orange-700  border-orange-200',
  Emergency: 'bg-red-50     text-red-700     border-red-200',
};

export function StatusBadge({ status, size = 'sm' }: StatusBadgeProps) {
  const style = STATUS_STYLES[status] ?? 'bg-gray-50 text-gray-600 border-gray-200';
  const sizeClass = size === 'md' ? 'px-2.5 py-1 text-sm' : 'px-2 py-0.5 text-xs';

  return (
    <span className={`inline-flex items-center rounded-full border font-medium ${sizeClass} ${style}`}>
      {status}
    </span>
  );
}

export function UrgencyBadge({ urgency }: { urgency: string }) {
  const style = URGENCY_STYLES[urgency] ?? 'bg-gray-50 text-gray-600 border-gray-200';

  return (
    <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${style}`}>
      {urgency}
    </span>
  );
}
