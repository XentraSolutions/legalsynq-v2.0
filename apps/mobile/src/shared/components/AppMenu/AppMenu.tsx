import { useEffect, useState } from 'react';
import { Dimensions, Modal, Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import { useAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';
import Animated, { useAnimatedStyle, useSharedValue, withTiming } from 'react-native-reanimated';

import type { MainStackParamList } from '@/navigation/types/navigation';
import { AuthenticationService } from '@/shared/services/Authentication';
import { accountModeAtom, type AccountMode } from '@/shared/state/atoms';
import { cx, FIGMA_COLORS, FIGMA_TEXT } from '@/shared/styles';
import { useAuth } from '@/shared/hooks';
import { useMenuSettings } from '@/shared/hooks/useMenuSettings';
import type { MenuVisibilityKey, MenuVisibilitySettings } from '@/shared/constants/menuSettings';

export interface AppMenuProps {
  visible: boolean;
  onClose: () => void;
}

type DirectRoute = keyof Pick<
  MainStackParamList,
  'Dashboard' | 'Cases' | 'Marketplace' | 'MyLiens' | 'Offers' | 'Settings'
>;
type MenuSectionId = 'management' | 'tools';

type MenuChild = {
  label: string;
  route?: DirectRoute;
  subtitle?: string;
  visibilityKey: MenuVisibilityKey;
};

type MenuSection = {
  id: MenuSectionId;
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  children: MenuChild[];
};

const MENU_SECTIONS: Record<AccountMode, MenuSection[]> = {
  selling: [
    {
      id: 'management',
      icon: 'file-tray-full-outline',
      label: 'Management',
      children: [
        {
          label: 'Task Manager',
          subtitle: 'Task workflows will be added in a future pass.',
          visibilityKey: 'taskManager',
        },
        { label: 'Cases', route: 'Cases', visibilityKey: 'cases' },
        { label: 'Liens', route: 'MyLiens', visibilityKey: 'liens' },
        {
          label: 'Bill of Sales',
          subtitle: 'Bill of sale management will be added in a future pass.',
          visibilityKey: 'billOfSales',
        },
        {
          label: 'Servicing',
          subtitle: 'Servicing tools will be added in a future pass.',
          visibilityKey: 'servicing',
        },
        {
          label: 'Contacts',
          subtitle: 'Contact management will be added in a future pass.',
          visibilityKey: 'contacts',
        },
      ],
    },
    {
      id: 'tools',
      icon: 'construct-outline',
      label: 'Tools & Utilities',
      children: [
        {
          label: 'Reports',
          subtitle: 'Reporting tools will be added in a future pass.',
          visibilityKey: 'reports',
        },
        {
          label: 'Batch Upload',
          subtitle: 'Batch upload workflows will be added in a future pass.',
          visibilityKey: 'batchUpload',
        },
        {
          label: 'Document Handling',
          subtitle: 'Document handling will be added in a future pass.',
          visibilityKey: 'documentHandling',
        },
        {
          label: 'User Management',
          subtitle: 'User management will be added in a future pass.',
          visibilityKey: 'userManagement',
        },
      ],
    },
  ],
  buying: [
    {
      id: 'management',
      icon: 'file-tray-full-outline',
      label: 'Management',
      children: [
        {
          label: 'Task Manager',
          subtitle: 'Task workflows will be added in a future pass.',
          visibilityKey: 'taskManager',
        },
        { label: 'Cases', route: 'Cases', visibilityKey: 'cases' },
        { label: 'Liens', route: 'Marketplace', visibilityKey: 'liens' },
        {
          label: 'Bill of Sales',
          subtitle: 'Bill of sale management will be added in a future pass.',
          visibilityKey: 'billOfSales',
        },
        {
          label: 'Servicing',
          subtitle: 'Servicing tools will be added in a future pass.',
          visibilityKey: 'servicing',
        },
        {
          label: 'Contacts',
          subtitle: 'Contact management will be added in a future pass.',
          visibilityKey: 'contacts',
        },
      ],
    },
    {
      id: 'tools',
      icon: 'construct-outline',
      label: 'Tools & Utilities',
      children: [
        {
          label: 'Reports',
          subtitle: 'Reporting tools will be added in a future pass.',
          visibilityKey: 'reports',
        },
        {
          label: 'Batch Upload',
          subtitle: 'Batch upload workflows will be added in a future pass.',
          visibilityKey: 'batchUpload',
        },
        {
          label: 'Document Handling',
          subtitle: 'Document handling will be added in a future pass.',
          visibilityKey: 'documentHandling',
        },
        {
          label: 'User Management',
          subtitle: 'User management will be added in a future pass.',
          visibilityKey: 'userManagement',
        },
      ],
    },
  ],
};

const CHILD_ROW_HEIGHT = 44;
const ACCOUNT_MODES: AccountMode[] = ['selling', 'buying'];

export function getVisibleAccountModes(menuVisibility: MenuVisibilitySettings): AccountMode[] {
  return ACCOUNT_MODES.filter((mode) => menuVisibility[mode]);
}

export function getVisibleMenuSections(
  accountMode: AccountMode,
  menuVisibility: MenuVisibilitySettings
): MenuSection[] {
  return MENU_SECTIONS[accountMode]
    .map((section) => ({
      ...section,
      children: section.children.filter((child) => menuVisibility[child.visibilityKey]),
    }))
    .filter((section) => section.children.length > 0);
}

export function AppMenu({ visible, onClose }: AppMenuProps) {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const [accountMode, setAccountMode] = useAtom(accountModeAtom);
  const [expandedSection, setExpandedSection] = useState<MenuSectionId | null>(null);
  const { colorScheme } = useNativeWindColorScheme();
  const insets = useSafeAreaInsets();
  const { user } = useAuth();
  const { settings: menuVisibility } = useMenuSettings();
  const isDark = colorScheme === 'dark';
  const stripWidth = Math.min(72, Dimensions.get('window').width * 0.17);
  const iconColor = isDark ? '#a8a9b0' : '#737681';
  const visibleAccountModes = getVisibleAccountModes(menuVisibility);
  const sections = getVisibleMenuSections(accountMode, menuVisibility);
  const fullName = user ? `${user.firstName} ${user.lastName}`.trim() : '';
  const initials = user ? `${user.firstName[0] ?? ''}${user.lastName[0] ?? ''}`.toUpperCase() : '';
  const role = user?.roles[0] ?? '';

  useEffect(() => {
    if (menuVisibility[accountMode]) return;
    const fallbackMode = visibleAccountModes[0];
    if (fallbackMode) setAccountMode(fallbackMode);
  }, [accountMode, menuVisibility, setAccountMode, visibleAccountModes]);

  const navigateToRoute = (route: DirectRoute) => {
    onClose();

    switch (route) {
      case 'Dashboard':
        navigation.navigate('Dashboard');
        break;
      case 'Cases':
        navigation.navigate('Cases');
        break;
      case 'Marketplace':
        navigation.navigate('Marketplace');
        break;
      case 'MyLiens':
        navigation.navigate('MyLiens');
        break;
      case 'Offers':
        navigation.navigate('Offers');
        break;
      case 'Settings':
        navigation.navigate('Settings');
        break;
    }
  };

  const navigateToChild = (child: MenuChild) => {
    if (child.route) {
      navigateToRoute(child.route);
      return;
    }

    onClose();
    navigation.navigate('Placeholder', {
      title: child.label,
      subtitle: child.subtitle,
    });
  };

  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 flex-row">
        <Pressable
          accessibilityRole="button"
          className="bg-black/25 dark:bg-black/80"
          style={{ width: stripWidth }}
          onPress={onClose}
        />
        <View style={{ flex: 1, paddingTop: insets.top }} className="bg-white dark:bg-[#18191d]">
          <View className="flex-1 px-8 pt-4">
            <View className="flex-row items-start justify-between">
              <View className="flex-row items-center">
                <View className="h-[48px] w-[48px] items-center justify-center rounded-full bg-[#4a423a]">
                  <Text className={cx(FIGMA_TEXT.avatarInitials, 'text-white')}>{initials}</Text>
                </View>
                <View className="ml-5">
                  <Text className={cx(FIGMA_TEXT.drawerName, 'text-[#202228] dark:text-white')}>
                    {fullName}
                  </Text>
                  <Text className={cx(FIGMA_TEXT.drawerRole, 'text-[#777984] dark:text-[#a5a6ae]')}>
                    {role}
                  </Text>
                </View>
              </View>
              <Pressable accessibilityRole="button" hitSlop={12} onPress={onClose}>
                <Ionicons color={isDark ? '#a8a9b0' : '#6f737d'} name="menu-outline" size={28} />
              </Pressable>
            </View>

            {visibleAccountModes.length > 0 ? (
              <View className="mt-11">
                <View className="min-h-[52px] flex-row items-center px-4">
                  <Ionicons color={iconColor} name="people-outline" size={24} />
                  <Text
                    className={cx(FIGMA_TEXT.drawerHeading, 'ml-5 text-[#202228] dark:text-white')}
                  >
                    Account Type
                  </Text>
                </View>

                <View className="ml-[27px] mt-2 border-l border-[#dedfe3] dark:border-[#2e3036]">
                  {visibleAccountModes.map((mode) => (
                    <ModeChoice
                      key={mode}
                      label={mode === 'selling' ? 'Selling' : 'Buying'}
                      selected={accountMode === mode}
                      onPress={() => {
                        setAccountMode(mode);
                        setExpandedSection(null);
                      }}
                    />
                  ))}
                </View>
              </View>
            ) : null}

            <View className="mt-8 gap-3">
              {menuVisibility.dashboard ? (
                <DirectMenuRow
                  active
                  icon="grid-outline"
                  iconColor={iconColor}
                  label="Dashboard"
                  onPress={() => navigateToRoute('Dashboard')}
                />
              ) : null}
              {sections.map((section) => (
                <ExpandableMenuSection
                  expanded={expandedSection === section.id}
                  iconColor={iconColor}
                  key={section.id}
                  section={section}
                  onChildPress={navigateToChild}
                  onToggle={() =>
                    setExpandedSection((current) => (current === section.id ? null : section.id))
                  }
                />
              ))}
            </View>

            <View className="mt-auto pb-4">
              <View className="mb-6 h-px bg-[#dedfe3] dark:bg-[#2e3036]" />

              {menuVisibility.settings ? (
                <DirectMenuRow
                  icon="settings-outline"
                  iconColor={iconColor}
                  label="Settings"
                  onPress={() => navigateToRoute('Settings')}
                />
              ) : null}

              <Pressable
                accessibilityRole="button"
                className="mt-4 min-h-[52px] flex-row items-center rounded-[14px] px-4"
                onPress={() => {
                  onClose();
                  void AuthenticationService.clearSession();
                }}
              >
                <Ionicons color="#ef4444" name="log-out-outline" size={22} />
                <Text className={cx(FIGMA_TEXT.drawerItem, 'ml-5 flex-1 text-[#ef4444]')}>
                  Logout
                </Text>
              </Pressable>
            </View>
          </View>
        </View>
      </View>
    </Modal>
  );
}

function ModeChoice({
  label,
  onPress,
  selected,
}: {
  label: string;
  onPress: () => void;
  selected: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className="min-h-[54px] flex-row items-center"
      onPress={onPress}
    >
      <View className={cx('mr-8 h-[44px] w-[2px]', selected ? 'bg-[#f97332]' : 'bg-transparent')} />
      <Text
        className={cx(
          FIGMA_TEXT.drawerChoice,
          'flex-1',
          selected ? 'text-[#f97332]' : 'text-[#202228] dark:text-white'
        )}
      >
        {label}
      </Text>
      {selected ? (
        <View className="h-8 w-8 items-center justify-center rounded-full bg-[#f97332]">
          <Ionicons color="#15161a" name="checkmark" size={21} />
        </View>
      ) : null}
    </Pressable>
  );
}

function DirectMenuRow({
  active,
  icon,
  iconColor,
  label,
  onPress,
}: {
  active?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  iconColor: string;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'min-h-[52px] flex-row items-center rounded-[14px] px-4',
        active && 'bg-[#fff0e8] dark:bg-[#2a211d]'
      )}
      onPress={onPress}
    >
      <Ionicons color={active ? FIGMA_COLORS.accent : iconColor} name={icon} size={22} />
      <Text
        className={cx(
          FIGMA_TEXT.drawerItem,
          'ml-5 flex-1',
          active ? 'text-[#f97332]' : 'text-[#202228] dark:text-white'
        )}
      >
        {label}
      </Text>
      <Ionicons
        color={active ? FIGMA_COLORS.accent : iconColor}
        name="chevron-forward-outline"
        size={20}
      />
    </Pressable>
  );
}

function ExpandableMenuSection({
  expanded,
  iconColor,
  section,
  onChildPress,
  onToggle,
}: {
  expanded: boolean;
  iconColor: string;
  section: MenuSection;
  onChildPress: (child: MenuChild) => void;
  onToggle: () => void;
}) {
  const expandedHeight = section.children.length * CHILD_ROW_HEIGHT + 8;
  const height = useSharedValue(expanded ? expandedHeight : 0);
  const opacity = useSharedValue(expanded ? 1 : 0);

  useEffect(() => {
    height.value = withTiming(expanded ? expandedHeight : 0, { duration: 180 });
    opacity.value = withTiming(expanded ? 1 : 0, { duration: 140 });
  }, [expanded, expandedHeight, height, opacity]);

  const animatedStyle = useAnimatedStyle(() => ({
    height: height.value,
    opacity: opacity.value,
  }));

  return (
    <View>
      <Pressable
        accessibilityRole="button"
        className={cx(
          'min-h-[52px] flex-row items-center rounded-[14px] px-4',
          expanded && 'bg-[#fff0e8] dark:bg-[#2a211d]'
        )}
        onPress={onToggle}
      >
        <Ionicons
          color={expanded ? FIGMA_COLORS.accent : iconColor}
          name={section.icon}
          size={22}
        />
        <Text
          className={cx(
            FIGMA_TEXT.drawerItem,
            'ml-5 flex-1',
            expanded ? 'text-[#f97332]' : 'text-[#202228] dark:text-white'
          )}
        >
          {section.label}
        </Text>
        <Ionicons
          color={expanded ? FIGMA_COLORS.accent : iconColor}
          name={expanded ? 'chevron-up-outline' : 'chevron-down-outline'}
          size={20}
        />
      </Pressable>

      <Animated.View style={[{ overflow: 'hidden' }, animatedStyle]}>
        <View className="ml-[27px] mt-2 border-l border-[#dedfe3] dark:border-[#2e3036]">
          {section.children.map((child) => (
            <MenuChildChoice child={child} key={child.label} onPress={() => onChildPress(child)} />
          ))}
        </View>
      </Animated.View>
    </View>
  );
}

function MenuChildChoice({ child, onPress }: { child: MenuChild; onPress: () => void }) {
  return (
    <Pressable
      accessibilityRole="button"
      className="min-h-[44px] flex-row items-center rounded-xl"
      onPress={onPress}
    >
      <View className="mr-8 h-[34px] w-[2px] bg-transparent" />
      <Text className="flex-1 font-jakarta-medium text-[13px] text-[#383b44] dark:text-[#d8d9dd]">
        {child.label}
      </Text>
    </Pressable>
  );
}
