import { Text, View } from 'react-native';

import { Avatar } from '@/shared/components/Avatar';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import type { UserSession } from '@/shared/types/auth';

export function ProfileHeader({ user }: { user: UserSession }) {
  return (
    <View className="items-center px-5 pt-8">
      <Avatar name={`${user.firstName} ${user.lastName}`} size="xl" />
      <Text className={cx(FIGMA_TEXT.rowValue, 'mt-2 text-[#f97332]')}>Change Photo</Text>
      <Text className="mt-4 font-jakarta-semibold text-[24px] leading-[30px] text-[#202228] dark:text-white">
        {user.firstName} {user.lastName}
      </Text>
      <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>{user.email}</Text>
      <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-content-tertiary dark:text-[#8f929b]')}>{user.organization.name}</Text>
    </View>
  );
}
