import { useEffect, useState } from 'react';
import Animated, {
  cancelAnimation,
  Easing,
  interpolate,
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withTiming,
} from 'react-native-reanimated';
import { View, type DimensionValue, type LayoutChangeEvent, type ViewStyle } from 'react-native';

const SHIMMER_HIGHLIGHT = 'rgba(255, 255, 255, 0.42)';

export interface SkeletonProps {
  width: DimensionValue;
  height: DimensionValue;
  borderRadius?: number;
  variant?: 'rect' | 'circle' | 'text';
}

export function Skeleton({ width, height, borderRadius = 8, variant = 'rect' }: SkeletonProps) {
  const [layoutWidth, setLayoutWidth] = useState(0);
  const progress = useSharedValue(0);

  useEffect(() => {
    if (layoutWidth === 0) {
      return undefined;
    }

    progress.value = 0;
    progress.value = withRepeat(
      withTiming(1, { duration: 1400, easing: Easing.inOut(Easing.ease) }),
      -1,
      false
    );

    return () => cancelAnimation(progress);
  }, [layoutWidth, progress]);

  const shimmerWidth = Math.max(layoutWidth * 0.55, 48);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [
      {
        translateX: interpolate(progress.value, [0, 1], [-shimmerWidth, layoutWidth]),
      },
      { skewX: '-16deg' },
    ],
  }));
  const skeletonStyle: ViewStyle = {
    width,
    height,
    borderRadius: variant === 'circle' ? 9999 : variant === 'text' ? 4 : borderRadius,
    overflow: 'hidden',
  };

  const handleLayout = (event: LayoutChangeEvent) => {
    const nextWidth = event.nativeEvent.layout.width;
    if (nextWidth !== layoutWidth) {
      setLayoutWidth(nextWidth);
    }
  };

  return (
    <View
      accessibilityElementsHidden
      accessible={false}
      className="bg-[#e4e5e9] dark:bg-[#2a2b30]"
      importantForAccessibility="no-hide-descendants"
      style={skeletonStyle}
      testID="skeleton"
      onLayout={handleLayout}
    >
      {layoutWidth > 0 ? (
        <Animated.View
          pointerEvents="none"
          style={[
            {
              backgroundColor: SHIMMER_HIGHLIGHT,
              bottom: 0,
              position: 'absolute',
              top: 0,
              width: shimmerWidth,
            },
            animatedStyle,
          ]}
          testID="skeleton-shimmer"
        />
      ) : null}
    </View>
  );
}
