import { render, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { SynqLienDashboardLanding } from './synqlien-dashboard-landing';

describe('SynqLienDashboardLanding', () => {
  test('renders the static funding-company dashboard without live data', () => {
    render(<SynqLienDashboardLanding />);

    expect(screen.getByRole('heading', { name: 'Dashboard', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Total Lien Pending')).toBeInTheDocument();
    expect(screen.getByText('$2,450,000.00')).toBeInTheDocument();
    expect(screen.getByText('Greenfield Medical Center')).toBeInTheDocument();
    expect(screen.getByText('No Appointments Today')).toBeInTheDocument();
    expect(screen.getByText('Funding Company Performance')).toBeInTheDocument();
    expect(screen.getByText('Meridian Capital Group')).toBeInTheDocument();
    expect(screen.getByText('Review and accept incoming lien offers.')).toBeInTheDocument();
  });
});
