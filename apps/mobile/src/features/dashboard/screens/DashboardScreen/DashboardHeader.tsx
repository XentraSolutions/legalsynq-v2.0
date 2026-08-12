import { Text, View } from 'react-native';
import type { AccountMode } from '@/shared/state/atoms';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { useAuth } from '@/shared';
import { CircleButton } from './CircleButton';

export function DashboardHeader({
  accountMode,
  isDark,
  onOpenMenu,
  onOpenXenia,
  showXenia,
}: {
  accountMode: AccountMode;
  isDark: boolean;
  onOpenMenu: () => void;
  onOpenXenia: () => void;
  showXenia: boolean;
}) {
  const { user } = useAuth();
  const userName = user ? `${user.firstName}`.trim() : '';
  const subtitle = accountMode === 'selling' ? 'Lien selling dashboard' : 'Lien buying dashboard';
  const iconColor = isDark ? '#a1a1aa' : '#6f737d';

  return (
    <View className="mt-2 flex-row items-center">
      <CircleButton
        icon="menu-outline"
        iconColor={iconColor}
        isDark={isDark}
        onPress={onOpenMenu}
      />
      <View className="ml-3 flex-1">
        <Text className={cx(TYPE.dashboardGreeting, 'text-[#1f2329] dark:text-white')}>
          Welcome, {userName}
        </Text>
        <Text className={cx(TYPE.dashboardSubtitle, 'mt-0.5 text-[#8a8d96] dark:text-[#8d9099]')}>
          {subtitle}
        </Text>
      </View>
      <View className="flex-row gap-2">
        {showXenia ? (
          <CircleButton
            accessibilityLabel="Open Xenia AI"
            accent
            icon="sparkles"
            iconColor="white"
            isDark={isDark}
            onPress={onOpenXenia}
          />
        ) : null}
        <CircleButton icon="search-outline" iconColor={iconColor} isDark={isDark} />
        <CircleButton dot icon="notifications-outline" iconColor={iconColor} isDark={isDark} />
      </View>
    </View>
  );
}
