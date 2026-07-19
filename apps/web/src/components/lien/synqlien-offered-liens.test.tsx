import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';
import { SynqLienOfferedLiens } from './synqlien-offered-liens';

describe('SynqLienOfferedLiens', () => {
  test('renders the static offered liens table and local filters', () => {
    render(<SynqLienOfferedLiens />);

    expect(screen.getByRole('heading', { name: 'Offered Liens', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('Track and evaluate lien opportunities submitted directly to your portal.')).toBeInTheDocument();
    expect(screen.getByText('LN-40218')).toBeInTheDocument();
    expect(screen.getByText('Angela Morrison')).toBeInTheDocument();
    expect(screen.getByText('of 200 entries.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Declined' }));

    expect(screen.getByText('LN-40226')).toBeInTheDocument();
    expect(screen.queryByText('LN-40218')).not.toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText('Search...'), {
      target: { value: 'Thomas' },
    });

    expect(screen.getByText('Thomas Nguyen')).toBeInTheDocument();
  });
});
