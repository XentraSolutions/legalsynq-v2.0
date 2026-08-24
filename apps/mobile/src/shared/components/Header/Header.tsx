import type { ReactNode } from 'react';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import { AppMenu } from '@/shared/components/AppMenu';
import { cx, FIGMA_COLORS, FIGMA_TEXT } from '@/shared/styles';

export interface HeaderProps {
  title: string;
  subtitle?: string;
  showBack?: boolean;
  showMenu?: boolean;
  onBack?: () => void;
  rightAction?: ReactNode;
  rightActionContainerClassName?: string;
}

export function Header({
  title,
  subtitle,
  showBack = false,
  showMenu,
  onBack,
  rightAction,
  rightActionContainerClassName,
}: HeaderProps) {
  const [menuVisible, setMenuVisible] = useState(false);
  const { colorScheme } = useNativeWindColorScheme();
  const isDark = colorScheme === 'dark';
  const shouldShowMenu = showMenu ?? !showBack;
  const iconColor = isDark ? '#a1a1aa' : '#6f737d';

  return (
    <SafeAreaView edges={['top']} className="bg-[#f7f7f8] dark:bg-[#050506]">
      <View className="h-16 flex-row items-center px-4">
        <View className="w-10">
          {showBack ? (
            <Pressable
              accessibilityRole="button"
              className="h-9 w-9 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]"
              hitSlop={12}
              onPress={onBack}
            >
              <Ionicons color={FIGMA_COLORS.accent} name="chevron-back" size={24} />
            </Pressable>
          ) : shouldShowMenu ? (
            <Pressable
              accessibilityRole="button"
              className="h-9 w-9 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]"
              onPress={() => setMenuVisible(true)}
            >
              <Ionicons color={iconColor} name="menu-outline" size={20} />
            </Pressable>
          ) : null}
        </View>
        <View className={cx('flex-1', showBack ? 'items-center' : 'ml-3 items-start')}>
          <Text
            className={cx(FIGMA_TEXT.cardTitle, 'text-[#202228] dark:text-white')}
            numberOfLines={1}
          >
            {title}
          </Text>
          {subtitle ? (
            <Text
              className={cx(
                FIGMA_TEXT.dashboardSubtitle,
                'mt-0.5 text-[#8a8d96] dark:text-[#8d9099]'
              )}
              numberOfLines={1}
            >
              {subtitle}
            </Text>
          ) : null}
        </View>
        <View className={cx('w-10 items-end', rightActionContainerClassName)}>{rightAction}</View>
      </View>
      {shouldShowMenu ? (
        <AppMenu visible={menuVisible} onClose={() => setMenuVisible(false)} />
      ) : null}
    </SafeAreaView>
  );
}
