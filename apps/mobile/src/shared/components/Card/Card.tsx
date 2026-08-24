import type { ReactNode } from 'react';
import { Pressable, View, type StyleProp, type ViewStyle } from 'react-native';

import { cx } from '@/shared/styles';

export interface CardProps {
  children: ReactNode;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
  className?: string;
}

export function Card({ children, onPress, style, className }: CardProps) {
  const cardClass = cx('rounded-[16px] bg-white p-5 shadow-sm dark:bg-[#191a1f]', className);

  if (!onPress) {
    return (
      <View className={cardClass} style={style}>
        {children}
      </View>
    );
  }

  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      className={cardClass}
      style={({ pressed }) => [{ transform: [{ scale: pressed ? 0.99 : 1 }] }, style]}
    >
      {children}
    </Pressable>
  );
}
