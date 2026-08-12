'use client';

import { render, screen } from '@testing-library/react';
import { describe, test, expect, vi, beforeEach } from 'vitest';
import { DateDisplay } from './date-display';
import { useTimezone } from '@/lib/use-timezone';

// Mock the useTimezone hook
vi.mock('@/lib/use-timezone', () => ({
  useTimezone: vi.fn(() => 'UTC'),
  useBrowserTimezone: vi.fn(() => 'UTC'),
}));

describe('DateDisplay', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  test('renders null/undefined as fallback', () => {
    const { rerender } = render(<DateDisplay value={null} />);
    expect(screen.getByText('—')).toBeInTheDocument();

    rerender(<DateDisplay value={undefined} />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  test('renders custom fallback when provided', () => {
    render(<DateDisplay value={null} fallback="No date" />);
    expect(screen.getByText('No date')).toBeInTheDocument();
  });

  test('formats ISO string as datetime by default (legacy MM/DD/YYYY)', () => {
    render(<DateDisplay value="2024-09-15T14:30:00Z" />);
    const result = screen.queryByText(/09\/15\/2024/);
    expect(result).toBeInTheDocument();
  });

  test('formats ISO string as date only with format prop (legacy MM/DD/YYYY default)', () => {
    render(<DateDisplay value="2024-09-15T14:30:00Z" format="date" />);
    const text = screen.getByText('09/15/2024');
    expect(text).toBeInTheDocument();
  });

  test('accepts Date object as value', () => {
    const date = new Date('2024-09-15T14:30:00Z');
    render(<DateDisplay value={date} format="date" />);
    expect(screen.getByText('09/15/2024')).toBeInTheDocument();
  });

  test('uses tenant timezone from context by default', () => {
    const mockUseTimezone = useTimezone as ReturnType<typeof vi.fn>;
    mockUseTimezone.mockReturnValue('America/New_York');

    render(<DateDisplay value="2024-09-15T14:30:00Z" format="date" />);
    // 14:30 UTC is 10:30 EDT — same calendar day in America/New_York.
    expect(screen.getByText('09/15/2024')).toBeInTheDocument();
  });

  test('allows timezone override', () => {
    render(<DateDisplay value="2024-09-15T14:30:00Z" format="date" timezone="UTC" />);
    expect(screen.getByText('09/15/2024')).toBeInTheDocument();
  });

  test('gracefully handles invalid date strings', () => {
    render(<DateDisplay value="not-a-date" />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  test('pure date-only values never shift day, even in a negative-offset timezone', () => {
    const mockUseTimezone = useTimezone as ReturnType<typeof vi.fn>;
    mockUseTimezone.mockReturnValue('Pacific/Midway'); // UTC-11, worst case for day-shift
    render(<DateDisplay value="2024-01-15" format="date" />);
    expect(screen.getByText('01/15/2024')).toBeInTheDocument();
  });

  test('a real timestamp with format="date" resolves to the correct calendar day per timezone', () => {
    const mockUseTimezone = useTimezone as ReturnType<typeof vi.fn>;
    mockUseTimezone.mockReturnValue('Pacific/Midway'); // UTC-11
    // 2024-01-15T05:00:00Z is still 2024-01-14 in UTC-11 — this SHOULD shift,
    // unlike a pure date-only value, because it's a genuine instant in time.
    render(<DateDisplay value="2024-01-15T05:00:00Z" format="date" />);
    expect(screen.getByText('01/14/2024')).toBeInTheDocument();
  });

  test('treats a naive datetime (no offset) as UTC rather than local time', () => {
    render(<DateDisplay value="2024-09-15T23:30:00" format="datetime" timezone="UTC" />);
    expect(screen.getByText('09/15/2024, 11:30 PM')).toBeInTheDocument();
  });
});
