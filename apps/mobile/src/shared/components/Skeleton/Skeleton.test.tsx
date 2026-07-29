import { fireEvent, render } from '@testing-library/react-native';

import { Skeleton } from './Skeleton';

describe('Skeleton', () => {
  it('is hidden from accessibility and starts its shimmer after layout', () => {
    const { getByTestId, queryByTestId } = render(<Skeleton height={20} width="100%" />);
    const skeleton = getByTestId('skeleton', { includeHiddenElements: true });

    expect(skeleton.props.accessible).toBe(false);
    expect(skeleton.props.accessibilityElementsHidden).toBe(true);
    expect(skeleton.props.importantForAccessibility).toBe('no-hide-descendants');
    expect(queryByTestId('skeleton-shimmer', { includeHiddenElements: true })).toBeNull();

    fireEvent(skeleton, 'layout', {
      nativeEvent: { layout: { height: 20, width: 240, x: 0, y: 0 } },
    });

    expect(getByTestId('skeleton-shimmer', { includeHiddenElements: true })).toBeTruthy();
  });

  it('preserves the requested dimensions and variant radius', () => {
    const { getByTestId } = render(<Skeleton height={48} variant="circle" width={48} />);

    expect(getByTestId('skeleton', { includeHiddenElements: true }).props.style).toMatchObject({
      borderRadius: 9999,
      height: 48,
      width: 48,
    });
  });
});
