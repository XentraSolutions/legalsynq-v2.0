import type { ReactNode } from 'react';
import { Pressable, Text, View, type PressableProps } from 'react-native';

import { Spinner } from '@/shared/components/Spinner';
import { cx, FIGMA_COLORS, FIGMA_TEXT } from '@/shared/styles';

export interface ButtonProps extends Omit<PressableProps, 'children'> {
  label: string;
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  loading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
}

const VARIANT_CLASSES = {
  primary: {
    button: 'border border-[#f97332] bg-[#f97332]',
    text: 'text-[#111111]',
  },
  secondary: {
    button: 'border border-transparent bg-[#ececee] dark:bg-[#2a2b30]',
    text: 'text-[#555964] dark:text-[#e7e7e9]',
  },
  ghost: {
    button: 'bg-transparent border border-transparent',
    text: 'text-[#f97332]',
  },
  danger: {
    button: 'bg-error-600 border border-error-600',
    text: 'text-white',
  },
} as const;

const SIZE_CLASSES = {
  sm: 'h-9 px-4',
  md: 'h-11 px-5',
  lg: 'h-12 px-6',
} as const;

export function Button({
  label,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled,
  leftIcon,
  rightIcon,
  className,
  ...pressableProps
}: ButtonProps) {
  const isDisabled = disabled || loading;
  const variantClasses = VARIANT_CLASSES[variant];

  return (
    <Pressable
      accessibilityRole="button"
      disabled={isDisabled}
      className={cx(
        'items-center justify-center rounded-full',
        SIZE_CLASSES[size],
        variantClasses.button,
        isDisabled ? 'opacity-50' : 'active:opacity-90',
        className
      )}
      {...pressableProps}
    >
      {loading ? (
        <Spinner color={variant === 'secondary' || variant === 'ghost' ? FIGMA_COLORS.accent : '#111111'} />
      ) : (
        <View className="flex-row items-center justify-center gap-2">
          {leftIcon}
          <Text className={cx(FIGMA_TEXT.cta, variantClasses.text)}>{label}</Text>
          {rightIcon}
        </View>
      )}
    </Pressable>
  );
}
