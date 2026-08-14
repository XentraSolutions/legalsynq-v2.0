import type { ReactNode } from 'react';
import { View } from 'react-native';
import { FIGMA_COLORS } from '@/shared/styles';

export function CardShell({
  children,
  isDark,
  className,
}: {
  children: ReactNode;
  isDark: boolean;
  className?: string;
}) {
  return (
    <View
      className={['mt-5 rounded-[16px] bg-white p-5 dark:bg-[#191a1f]', className]
        .filter(Boolean)
        .join(' ')}
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.45,
        shadowRadius: 10,
        shadowOffset: { height: 4, width: 0 },
        elevation: 2,
      }}
    >
      {children}
    </View>
  );
}
