import { render } from '@testing-library/react-native';
import type { DonutSlice } from './dashboardShared';
import { sortDonutSlicesDescending } from './DonutCard';
import { DonutChart, getDonutCenterValueTextStyle } from './DonutChart';

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

describe('DonutChart', () => {
  it('keeps a long center amount on one readable line', () => {
    const { getByText } = render(
      <DonutChart centerCaption="Total A/R" centerValue="$117,375.0" slices={[]} />
    );

    expect(getByText('$117,375.0').props).toMatchObject({
      numberOfLines: 1,
      style: { fontSize: 16, lineHeight: 20 },
    });
  });

  it('uses controlled font sizes for progressively longer values', () => {
    expect(getDonutCenterValueTextStyle('$1.2M')).toBeUndefined();
    expect(getDonutCenterValueTextStyle('$123,456.0')).toEqual({ fontSize: 16, lineHeight: 20 });
    expect(getDonutCenterValueTextStyle('$123,456,789.0')).toEqual({
      fontSize: 13,
      lineHeight: 16,
    });
  });
});
