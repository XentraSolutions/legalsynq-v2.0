import type { DonutSlice } from './index';
import { sortDonutSlicesDescending } from './DonutCard';

describe('sortDonutSlicesDescending', () => {
  it('orders graph and itemized data from highest value to lowest without mutating input', () => {
    const slices: DonutSlice[] = [
      { label: 'Medium', value: 25, color: '#f97332' },
      { label: 'Lowest', value: 10, color: '#22c55e' },
      { label: 'Highest', value: 65, color: '#3b82f6' },
    ];

    expect(sortDonutSlicesDescending(slices).map((slice) => slice.label)).toEqual([
      'Highest',
      'Medium',
      'Lowest',
    ]);
    expect(slices.map((slice) => slice.label)).toEqual(['Medium', 'Lowest', 'Highest']);
  });
});
