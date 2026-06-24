import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useAtom } from 'jotai';
import { useNavigation } from '@react-navigation/native';

import { Card } from '@/shared/components/Card';
import { Chip } from '@/shared/components/Chip';
import { Divider } from '@/shared/components/Divider';
import { Header } from '@/shared/components/Header';
import { Switch } from '@/shared/components/Switch';
import { featureFlagsAtom } from '@/shared/state/atoms/featureFlagsAtom';
import { themeAtom } from '@/shared/state/atoms/themeAtom';
import type { ThemePreference } from '@/shared/types/common';

export function SettingsScreen() {
  const navigation = useNavigation();
  const [theme, setTheme] = useAtom(themeAtom);
  const [flags, setFlags] = useAtom(featureFlagsAtom);

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Settings" onBack={() => navigation.goBack()} />
      <View className="mx-5 mt-6">
        <Text className="mb-3 font-jakarta-semibold text-[14px] leading-[20px] text-[#6f737d] dark:text-[#a1a1aa]">Appearance</Text>
        <Card>
          <View className="flex-row items-center justify-between gap-3">
            <Text className="font-jakarta-medium text-[14px] leading-[20px] text-[#202228] dark:text-white">Theme</Text>
            <View className="flex-row gap-2">
              {(['light', 'dark', 'system'] as ThemePreference[]).map((option) => (
                <Chip
                  key={option}
                  label={option[0].toUpperCase() + option.slice(1)}
                  selected={theme === option}
                  onPress={() => setTheme(option)}
                />
              ))}
            </View>
          </View>
        </Card>
      </View>
      <View className="mx-5 mt-6">
        <Text className="mb-3 font-jakarta-semibold text-[14px] leading-[20px] text-[#6f737d] dark:text-[#a1a1aa]">Security</Text>
        <Card>
          <View className="flex-row items-center justify-between">
            <Text className="font-jakarta-medium text-[14px] leading-[20px] text-[#202228] dark:text-white">Biometric Login</Text>
            <Switch
              value={flags.enableBiometrics}
              onValueChange={(value) => setFlags((current) => ({ ...current, enableBiometrics: value }))}
            />
          </View>
          <Divider />
          <Pressable className="flex-row items-center justify-between py-1">
            <Text className="font-jakarta-medium text-[14px] leading-[20px] text-[#202228] dark:text-white">Change Password</Text>
            <Ionicons color="#94a3b8" name="chevron-forward" size={20} />
          </Pressable>
        </Card>
      </View>
      <View className="mx-5 mt-6">
        <Text className="mb-3 font-jakarta-semibold text-[14px] leading-[20px] text-[#6f737d] dark:text-[#a1a1aa]">About</Text>
        <Card>
          <View className="flex-row items-center justify-between">
            <Text className="font-jakarta-medium text-[14px] leading-[20px] text-[#202228] dark:text-white">App Version</Text>
            <Text className="font-jakarta-medium text-[11px] leading-[15px] text-content-tertiary dark:text-[#8f929b]">3.0.0</Text>
          </View>
          <Divider />
          {['Terms of Service', 'Privacy Policy'].map((item) => (
            <View key={item}>
              <Pressable className="flex-row items-center justify-between py-1">
                <Text className="font-jakarta-medium text-[14px] leading-[20px] text-[#202228] dark:text-white">{item}</Text>
                <Ionicons color="#94a3b8" name="chevron-forward" size={20} />
              </Pressable>
              {item === 'Terms of Service' ? <Divider /> : null}
            </View>
          ))}
        </Card>
      </View>
    </View>
  );
}
