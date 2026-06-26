import { Text, View } from 'react-native';

import { Avatar } from '@/shared/components/Avatar';
import type { ActivityItem } from '@/features/mockData';

export interface RecentActivityListProps {
  activities: ActivityItem[];
}

export function RecentActivityList({ activities }: RecentActivityListProps) {
  return (
    <View className="gap-3">
      {activities.map((activity) => (
        <View className="flex-row items-center" key={activity.id}>
          <Avatar name={activity.orgName} size="md" />
          <View className="ml-3 flex-1">
            <Text className="text-base text-content-primary">{activity.title}</Text>
            <Text className="text-sm text-content-secondary">{activity.subtitle}</Text>
          </View>
          <Text className="text-xs text-content-tertiary">{activity.time}</Text>
        </View>
      ))}
    </View>
  );
}
