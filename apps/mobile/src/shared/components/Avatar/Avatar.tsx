import { Image, Text, View } from 'react-native';

import { cx } from '@/shared/styles';

export interface AvatarProps {
  name?: string;
  imageUrl?: string;
  size?: 'sm' | 'md' | 'lg' | 'xl';
}

const SIZE_CLASSES = {
  sm: 'h-8 w-8',
  md: 'h-10 w-10',
  lg: 'h-12 w-12',
  xl: 'h-16 w-16',
} as const;

const TEXT_CLASSES = {
  sm: 'text-xs',
  md: 'text-sm',
  lg: 'text-base',
  xl: 'text-xl',
} as const;

function initials(name?: string): string {
  if (!name) {
    return 'LS';
  }

  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('');
}

export function Avatar({ name, imageUrl, size = 'md' }: AvatarProps) {
  const sizeClass = SIZE_CLASSES[size];

  if (imageUrl) {
    return <Image accessibilityLabel={name} className={`${sizeClass} rounded-full`} source={{ uri: imageUrl }} />;
  }

  return (
    <View className={cx(sizeClass, 'items-center justify-center rounded-full bg-[#fde7d9] dark:bg-[#402513]')}>
      <Text className={cx(TEXT_CLASSES[size], 'font-jakarta-semibold text-[#c9571b] dark:text-[#f97332]')}>{initials(name)}</Text>
    </View>
  );
}
