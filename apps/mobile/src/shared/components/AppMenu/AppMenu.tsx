import { Dimensions, Modal, Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import { useAtom } from 'jotai';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import type { MainStackParamList } from '@/navigation/types/navigation';
import { AuthenticationService } from '@/shared/services/Authentication';
import { accountModeAtom, type AccountMode } from '@/shared/state/atoms';
import { COLORS, cx, FIGMA_TEXT } from '@/shared/styles';

export interface AppMenuProps {
  visible: boolean;
  onClose: () => void;
}

type MenuItem = {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  route: keyof Pick<MainStackParamList, 'Cases' | 'Marketplace' | 'MyLiens' | 'Offers'>;
};

const MENU_ITEMS: Record<AccountMode, MenuItem[]> = {
  selling: [
    { icon: 'document-text-outline', label: 'Lien Management', route: 'MyLiens' },
    { icon: 'receipt-outline', label: 'Receivables', route: 'Offers' },
    { icon: 'trending-up-outline', label: 'Reports', route: 'Cases' },
  ],
  buying: [
    { icon: 'document-text-outline', label: 'Asset Tracking', route: 'Marketplace' },
    { icon: 'folder-outline', label: 'Operation & Intake', route: 'Cases' },
    { icon: 'analytics-outline', label: 'Analytics', route: 'Offers' },
  ],
};

export function AppMenu({ visible, onClose }: AppMenuProps) {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const [accountMode, setAccountMode] = useAtom(accountModeAtom);
  const { colorScheme } = useNativeWindColorScheme();
  const insets = useSafeAreaInsets();
  const isDark = colorScheme === 'dark';
  const stripWidth = Math.min(64, Dimensions.get('window').width * 0.16);
  const iconColor = isDark ? '#a8a9b0' : '#737681';

  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 flex-row">
        <Pressable
          accessibilityRole="button"
          className="bg-black/25 dark:bg-black/75"
          style={{ width: stripWidth }}
          onPress={onClose}
        />
        <View style={{ flex: 1, paddingTop: insets.top }} className="bg-white dark:bg-[#18191d]">
          <View className="flex-1 px-8 pt-4">
            <View className="flex-row items-start justify-between">
              <View className="flex-row items-center">
                <View className="h-[48px] w-[48px] items-center justify-center rounded-full bg-[#4a423a]">
                  <Text className={cx(FIGMA_TEXT.avatarInitials, 'text-white')}>JD</Text>
                </View>
                <View className="ml-5">
                  <Text className={cx(FIGMA_TEXT.drawerName, 'text-[#202228] dark:text-white')}>John Doe</Text>
                  <Text className={cx(FIGMA_TEXT.drawerRole, 'text-[#777984] dark:text-[#a5a6ae]')}>Admin</Text>
                </View>
              </View>
              <Pressable accessibilityRole="button" hitSlop={12} onPress={onClose}>
                <Ionicons color={isDark ? '#a8a9b0' : '#6f737d'} name="menu-outline" size={20} />
              </Pressable>
            </View>
            <View className='mt-4 p-4 bg-surface-tertiary dark:bg-surface-dark-tertiary rounded-lg'>
              <View className="flex-row items-center">
                <Ionicons color={iconColor} name="people-outline" size={20} />
                <Text className={cx(FIGMA_TEXT.drawerHeading, 'ml-5 text-[#202228] dark:text-white')}>Account Type</Text>
              </View>

              <View className="mt-4 ml-2 border-l border-[#dedfe3] dark:border-[#2e3036]">
                <ModeChoice
                  label="Selling"
                  selected={accountMode === 'selling'}
                  onPress={() => setAccountMode('selling')}
                />
                <ModeChoice
                  label="Buying"
                  selected={accountMode === 'buying'}
                  onPress={() => setAccountMode('buying')}
                />
              </View>
            </View>
            <View className="mt-8 gap-4">
              {MENU_ITEMS[accountMode].map((item) => (
                <Pressable
                  accessibilityRole="button"
                  className="dark:bg-surface-dark-tertiary px-4 py-4 rounded-lg flex-row items-center"
                  key={item.label}
                  onPress={() => {
                    onClose();
                    navigation.navigate(item.route);
                  }}
                >
                  <Ionicons color={iconColor} name={item.icon} size={20} />
                  <Text className={cx(FIGMA_TEXT.drawerItem, 'ml-5 flex-1 text-[#202228] dark:text-white')}>{item.label}</Text>
                  <Ionicons color={iconColor} name="chevron-forward-outline" size={20} />
                </Pressable>
              ))}
            </View>

            <View className="mt-auto pb-4">
              <View className="mb-6 h-px bg-[#dedfe3] dark:bg-[#2e3036]" />

              <Pressable
                accessibilityRole="button"
                className="mb-8 flex-row items-center"
                onPress={() => {
                  onClose();
                  navigation.navigate('Settings');
                }}
              >
                <Ionicons color={iconColor} name="settings-outline" size={20} />
                <Text className={cx(FIGMA_TEXT.drawerItem, 'ml-5 flex-1 text-[#202228] dark:text-white')}>Settings</Text>
                <Ionicons color={iconColor} name="chevron-forward-outline" size={20} />
              </Pressable>

              <Pressable
                accessibilityRole="button"
                className="flex-row items-center"
                onPress={() => {
                  onClose();
                  void AuthenticationService.clearSession();
                }}
              >
                <Ionicons color="#ef4444" name="log-out-outline" size={20} />
                <Text className={cx(FIGMA_TEXT.drawerItem, 'ml-5 flex-1 text-[#ef4444]')}>Logout</Text>
              </Pressable>
            </View>
          </View>
        </View>
      </View>
    </Modal>
  );
}

function ModeChoice({ label, onPress, selected }: { label: string; onPress: () => void; selected: boolean }) {
  return (
    <Pressable accessibilityRole="button" className="min-h-[54px] flex-row items-center" onPress={onPress}>
      <View className={cx('h-full w-[2px]', selected ? 'bg-info' : 'bg-transparent')} />
      <Text className={cx(FIGMA_TEXT.drawerChoice, 'ml-8 flex-1', selected ? `text-info` : 'text-[#202228] dark:text-white')}>
        {label}
      </Text>
      {selected ? (
        <Ionicons color={COLORS.info} name="checkmark" size={20} />
      ) : null}
    </Pressable>
  );
}
