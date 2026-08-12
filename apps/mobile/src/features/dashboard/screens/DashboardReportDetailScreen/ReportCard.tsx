import type { ReactNode } from 'react';
import { View } from 'react-native';
import { cx, FIGMA_COLORS } from '@/shared/styles';

export function ReportCard({
  children,
  className,
  isDark,
}: {
  children: ReactNode;
  className?: string;
  isDark: boolean;
}) {
  return (
    <View
      className={cx('items-center rounded-[20px] bg-white px-6 py-8 dark:bg-[#191a1f]', className)}
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.44,
        shadowRadius: 10,
        shadowOffset: { height: 4, width: 0 },
        elevation: 2,
      }}
    >
      {children}
    </View>
  );
}
