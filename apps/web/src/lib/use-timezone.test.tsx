'use client';

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { useBrowserTimezone } from './use-timezone';

function BrowserTimezoneProbe() {
  const timezone = useBrowserTimezone();
  return <div>{timezone}</div>;
}

describe('useBrowserTimezone', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  test('refreshes the browser timezone when the window regains focus', async () => {
    let currentTimezone = 'America/New_York';

    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation((() => ({
      resolvedOptions: () => ({ timeZone: currentTimezone }),
    })) as unknown as typeof Intl.DateTimeFormat);

    render(<BrowserTimezoneProbe />);

    await waitFor(() => {
      expect(screen.getByText('America/New_York')).toBeInTheDocument();
    });

    currentTimezone = 'America/Los_Angeles';
    fireEvent(window, new Event('focus'));

    await waitFor(() => {
      expect(screen.getByText('America/Los_Angeles')).toBeInTheDocument();
    });
  });
});
