import type { ReactNode } from 'react';
import { Text, View } from 'react-native';

import { Button } from '@/shared/components/Button';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: ReactNode;
  actionLabel?: string;
  onAction?: () => void;
}

export function EmptyState({ title, description, icon, actionLabel, onAction }: EmptyStateProps) {
  return (
    <View className="flex-1 items-center justify-center px-6 py-10">
      {icon ? <View className="mb-4 h-16 w-16 items-center justify-center">{icon}</View> : null}
      <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-center text-[#202228] dark:text-white')}>{title}</Text>
      {description ? (
        <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-center text-[#6f737d] dark:text-[#a1a1aa]')}>{description}</Text>
      ) : null}
      {actionLabel && onAction ? (
        <Button className="mt-5" label={actionLabel} onPress={onAction} variant="secondary" />
      ) : null}
    </View>
  );
}
