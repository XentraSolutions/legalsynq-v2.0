import { useEffect } from 'react';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withTiming,
} from 'react-native-reanimated';
import type { DimensionValue, ViewStyle } from 'react-native';

export interface SkeletonProps {
  width: DimensionValue;
  height: DimensionValue;
  borderRadius?: number;
  variant?: 'rect' | 'circle' | 'text';
}

export function Skeleton({ width, height, borderRadius = 8, variant = 'rect' }: SkeletonProps) {
  const opacity = useSharedValue(0.3);

  useEffect(() => {
    opacity.value = withRepeat(withTiming(0.8, { duration: 900 }), -1, true);
  }, [opacity]);

  const animatedStyle = useAnimatedStyle(() => ({
    opacity: opacity.value,
  }));
  const skeletonStyle: ViewStyle = {
    width,
    height,
    borderRadius: variant === 'circle' ? 9999 : variant === 'text' ? 4 : borderRadius,
  };

  return (
    <Animated.View
      className="bg-slate-200"
      style={[skeletonStyle, animatedStyle]}
    />
  );
}
