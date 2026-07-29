import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { ProfileHeader } from '@/features/profile/components';
import { useProfile } from '@/features/profile/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Divider } from '@/shared/components/Divider';
import { Header } from '@/shared/components/Header';
import { AuthenticationService } from '@/shared/services/Authentication';

export function ProfileScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const { user } = useProfile();

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header
        rightAction={
          <Pressable accessibilityRole="button" onPress={() => navigation.navigate('Settings')}>
            <Ionicons color="#f97332" name="settings-outline" size={24} />
          </Pressable>
        }
        subtitle="Account preferences and security"
        title="Profile"
      />
      <ProfileHeader user={user} />
      <Divider />
      <View className="mx-5 mt-4 gap-1">
        {[
          { label: 'Change Password', danger: false },
          { label: 'Notification Prefs', danger: false },
          { label: 'Sign Out', danger: true },
        ].map((item) => (
          <Pressable
            accessibilityRole="button"
            className="flex-row items-center justify-between rounded-[14px] bg-white px-4 py-4 dark:bg-[#191a1f]"
            key={item.label}
            onPress={() => {
              if (item.danger) {
                void AuthenticationService.logout();
              }
            }}
          >
            <Text className={`font-jakarta-medium text-[14px] leading-[20px] ${item.danger ? 'text-error-600' : 'text-[#202228] dark:text-white'}`}>
              {item.label}
            </Text>
            {!item.danger ? <Ionicons color="#94a3b8" name="chevron-forward" size={20} /> : null}
          </Pressable>
        ))}
      </View>
    </View>
  );
}
