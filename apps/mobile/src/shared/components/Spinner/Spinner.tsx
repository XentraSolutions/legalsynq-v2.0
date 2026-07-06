import { ActivityIndicator } from 'react-native';

import { COLORS } from '@/shared/styles/tokens';

export interface SpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  color?: string;
}

const SIZE_MAP = {
  sm: 16,
  md: 24,
  lg: 32,
} as const;

export function Spinner({ size = 'md', color = COLORS.primary }: SpinnerProps) {
  return <ActivityIndicator animating={true} color={color} size={SIZE_MAP[size]} />;
}
