import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, test, expect, beforeEach, vi } from 'vitest';
import { BaseSelect, type BaseSelectOption } from './base-select';

// jsdom has no IntersectionObserver. This fake records every `observe()`
// call so tests can assert *what* got observed (catches the regression
// where the effect fired before Radix had mounted the sentinel into the
// DOM, so it observed nothing) and can manually fire intersection entries
// to drive the infinite-scroll callback without a real scrollable viewport.
class FakeIntersectionObserver implements IntersectionObserver {
  static instances: FakeIntersectionObserver[] = [];
  readonly root = null;
  readonly rootMargin = '';
  readonly scrollMargin = '';
  readonly thresholds: ReadonlyArray<number> = [];
  observedTargets: Element[] = [];

  constructor(private callback: IntersectionObserverCallback) {
    FakeIntersectionObserver.instances.push(this);
  }

  observe(target: Element) {
    this.observedTargets.push(target);
  }
  unobserve(target: Element) {
    this.observedTargets = this.observedTargets.filter((t) => t !== target);
  }
  disconnect() {
    this.observedTargets = [];
  }
  takeRecords(): IntersectionObserverEntry[] {
    return [];
  }

  /** Simulates the sentinel scrolling into (or out of) view. */
  fire(isIntersecting: boolean) {
    const target = this.observedTargets[0];
    this.callback(
      [{ isIntersecting, target } as IntersectionObserverEntry],
      this,
    );
  }
}

const OPTIONS: BaseSelectOption[] = [
  { value: '1', label: 'Anderson & Ashworth Law Firm' },
  { value: '2', label: 'Anderson & Baxter Law Offices' },
  { value: '3', label: 'Anderson & Hastings Law Group' },
];

async function openDropdown() {
  const user = userEvent.setup();
  await user.click(screen.getByRole('button', { name: /select/i }));
  return user;
}

describe('BaseSelect infinite scroll', () => {
  beforeEach(() => {
    FakeIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', FakeIntersectionObserver);
  });

  test('observes the sentinel once the list is open, and fetches the next page on intersect', async () => {
    const onLoadMore = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={() => {}}
        options={OPTIONS}
        loadingMode="infinite"
        hasNextPage
        isFetchingMore={false}
        onLoadMore={onLoadMore}
        placeholder="Select law firm..."
      />,
    );

    await openDropdown();
    await screen.findByRole('listbox');

    // Regression guard: before the fix, the effect ran before Radix
    // committed the sentinel into the DOM, so `observe()` was never called
    // with a real target and this list would stay empty forever.
    await waitFor(() => {
      expect(FakeIntersectionObserver.instances).toHaveLength(1);
      expect(FakeIntersectionObserver.instances[0].observedTargets).toHaveLength(1);
    });

    expect(onLoadMore).not.toHaveBeenCalled();
    FakeIntersectionObserver.instances[0].fire(true);
    expect(onLoadMore).toHaveBeenCalledTimes(1);
  });

  test('does not fetch again while a page is already loading', async () => {
    const onLoadMore = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={() => {}}
        options={OPTIONS}
        loadingMode="infinite"
        hasNextPage
        isFetchingMore
        onLoadMore={onLoadMore}
        placeholder="Select law firm..."
      />,
    );

    await openDropdown();
    await screen.findByRole('listbox');

    await waitFor(() => {
      expect(FakeIntersectionObserver.instances).toHaveLength(1);
    });

    FakeIntersectionObserver.instances[0].fire(true);
    expect(onLoadMore).not.toHaveBeenCalled();
  });

  test('does not observe anything once every page has been loaded', async () => {
    const onLoadMore = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={() => {}}
        options={OPTIONS}
        loadingMode="infinite"
        hasNextPage={false}
        isFetchingMore={false}
        onLoadMore={onLoadMore}
        placeholder="Select law firm..."
      />,
    );

    await openDropdown();
    await screen.findByRole('listbox');

    expect(FakeIntersectionObserver.instances).toHaveLength(0);
  });
});

// The most common usage across the app (e.g. the medical-code picker in
// medical-codes-description.tsx): a plain popover select with the full
// option list handed in up front, client-side search filtering, and an
// inline "+ Add New…" row.
describe('BaseSelect eager single-select', () => {
  test('filters options client-side as the user types', async () => {
    render(
      <BaseSelect
        value={null}
        onChange={() => {}}
        options={OPTIONS}
        placeholder="Select a code"
        searchPlaceholder="Search codes..."
      />,
    );

    const user = await openDropdown();
    expect(screen.getAllByRole('option')).toHaveLength(3);

    await user.type(screen.getByPlaceholderText('Search codes...'), 'Baxter');

    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(1);
    expect(options[0]).toHaveTextContent('Anderson & Baxter Law Offices');
  });

  test('selecting an option reports it and closes the popover', async () => {
    const onChange = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={onChange}
        options={OPTIONS}
        placeholder="Select a code"
      />,
    );

    const user = await openDropdown();
    await user.click(screen.getByRole('option', { name: /Anderson & Hastings Law Group/ }));

    expect(onChange).toHaveBeenCalledWith('3', OPTIONS[2]);
    // Closing the popover unmounts the listbox from the accessibility tree.
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
  });

  test('selected value renders as the trigger label', () => {
    render(
      <BaseSelect
        value="2"
        onChange={() => {}}
        options={OPTIONS}
        placeholder="Select a code"
      />,
    );

    expect(
      screen.getByRole('button', { name: 'Anderson & Baxter Law Offices' }),
    ).toBeInTheDocument();
  });

  test('clearable single-select emits an empty value without leaving stale selection', async () => {
    const onChange = vi.fn();

    render(
      <BaseSelect
        value="2"
        clearable
        onChange={onChange}
        options={OPTIONS}
        placeholder="Select a code"
      />,
    );

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /Anderson & Baxter Law Offices/ }));
    await user.click(screen.getByLabelText('Clear selection'));

    expect(onChange).toHaveBeenCalledWith('', OPTIONS[1]);
  });

  test('the create-action row hands off to the caller without rendering its own modal', async () => {
    const onSelect = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={() => {}}
        options={OPTIONS}
        placeholder="Select a code"
        createAction={{ label: 'Add New Medical Code', onSelect }}
      />,
    );

    const user = await openDropdown();
    await user.click(screen.getByRole('button', { name: 'Add New Medical Code' }));

    expect(onSelect).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
  });

  test('arrow keys move the highlighted option and Enter selects it', async () => {
    const onChange = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={onChange}
        options={OPTIONS}
        placeholder="Select a code"
      />,
    );

    const user = await openDropdown();
    // activeIndex starts at 0 (the first option); one ArrowDown moves the
    // highlight to the second.
    await user.keyboard('{ArrowDown}{Enter}');

    expect(onChange).toHaveBeenCalledWith('2', OPTIONS[1]);
  });

  test('Escape closes the popover without selecting anything', async () => {
    const onChange = vi.fn();

    render(
      <BaseSelect
        value={null}
        onChange={onChange}
        options={OPTIONS}
        placeholder="Select a code"
      />,
    );

    const user = await openDropdown();
    await user.keyboard('{Escape}');

    expect(onChange).not.toHaveBeenCalled();
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
  });
});

// The other structurally distinct mode: `inline` + `multiple` +
// `showCheckboxes`, used by the Liens filter modal (liens-filter.tsx) to
// render an always-visible checkbox list with no trigger button or Popover
// at all — a different rendering branch than the two describe blocks above.
describe('BaseSelect inline multi-select', () => {
  function ControlledMultiSelect() {
    const [value, setValue] = useState<string[]>([]);
    return (
      <BaseSelect
        multiple
        inline
        showCheckboxes
        options={OPTIONS}
        value={value}
        onChange={(values) => setValue(values)}
        searchPlaceholder="Search..."
      />
    );
  }

  test('renders the list directly with no trigger button or popover', () => {
    render(
      <BaseSelect
        multiple
        inline
        showCheckboxes
        options={OPTIONS}
        value={[]}
        onChange={() => {}}
      />,
    );

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByRole('listbox')).toBeInTheDocument();
    expect(screen.getAllByRole('option')).toHaveLength(3);
  });

  test('clicking options accumulates them, and clicking again deselects', async () => {
    const user = userEvent.setup();
    render(<ControlledMultiSelect />);

    const first = screen.getByRole('option', { name: /Anderson & Ashworth Law Firm/ });
    const second = screen.getByRole('option', { name: /Anderson & Baxter Law Offices/ });

    await user.click(first);
    expect(first).toHaveAttribute('aria-selected', 'true');
    expect(second).toHaveAttribute('aria-selected', 'false');

    await user.click(second);
    expect(first).toHaveAttribute('aria-selected', 'true');
    expect(second).toHaveAttribute('aria-selected', 'true');

    // Toggling a selected option off again — the list stays open the whole
    // time (unlike single-select, which closes on pick).
    await user.click(first);
    expect(first).toHaveAttribute('aria-selected', 'false');
    expect(second).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('listbox')).toBeInTheDocument();
  });

  test('search filters the always-visible list without a server round-trip', async () => {
    const user = userEvent.setup();
    render(<ControlledMultiSelect />);

    await user.type(screen.getByPlaceholderText('Search...'), 'Hastings');

    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(1);
    expect(options[0]).toHaveTextContent('Anderson & Hastings Law Group');
  });
});
