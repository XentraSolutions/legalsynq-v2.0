import { format, formatDistanceToNowStrict, parseISO } from 'date-fns';

export function formatDisplayDate(value: string, pattern = 'MMM d, yyyy'): string {
  return format(parseISO(value), pattern);
}

export function formatRelativeDate(value: string): string {
  return formatDistanceToNowStrict(parseISO(value), { addSuffix: true });
}
