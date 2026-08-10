import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { ColumnDef, SortingState } from '@tanstack/react-table';
import { BaseTable } from './base-table';

interface RowData {
  id: string;
  name: string;
}

const columns: ColumnDef<RowData>[] = [
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Name',
  },
];

const data: RowData[] = [
  { id: '1', name: 'Beta' },
  { id: '2', name: 'Alpha' },
];

function SortingHarness() {
  const [sorting, setSorting] = useState<SortingState>([]);

  return (
    <div>
      <div data-testid="sort-state">{JSON.stringify(sorting)}</div>
      <BaseTable<RowData>
        data={data}
        columns={columns}
        sorting={sorting}
        onSortingChange={setSorting}
      />
    </div>
  );
}

describe('BaseTable sorting', () => {
  it('cycles through unsorted -> asc -> desc -> unsorted on repeated header clicks', async () => {
    const user = userEvent.setup();
    render(<SortingHarness />);

    const header = screen.getByRole('columnheader', { name: 'Name' });

    await user.click(header);
    expect(screen.getByTestId('sort-state')).toHaveTextContent(
      JSON.stringify([{ id: 'name', desc: false }]),
    );

    await user.click(header);
    expect(screen.getByTestId('sort-state')).toHaveTextContent(
      JSON.stringify([{ id: 'name', desc: true }]),
    );

    await user.click(header);
    expect(screen.getByTestId('sort-state')).toHaveTextContent('[]');
  });

  it('always sorts ascending first regardless of column value type', async () => {
    const user = userEvent.setup();

    interface NumericRow {
      id: string;
      amount: number;
    }

    function NumericHarness() {
      const [sorting, setSorting] = useState<SortingState>([]);
      return (
        <div>
          <div data-testid="sort-state">{JSON.stringify(sorting)}</div>
          <BaseTable<NumericRow>
            data={[{ id: '1', amount: 200 }, { id: '2', amount: 100 }]}
            columns={[{ id: 'amount', accessorKey: 'amount', header: 'Amount' }]}
            sorting={sorting}
            onSortingChange={setSorting}
          />
        </div>
      );
    }

    render(<NumericHarness />);
    const header = screen.getByRole('columnheader', { name: 'Amount' });

    await user.click(header);
    expect(screen.getByTestId('sort-state')).toHaveTextContent(
      JSON.stringify([{ id: 'amount', desc: false }]),
    );
  });
});
