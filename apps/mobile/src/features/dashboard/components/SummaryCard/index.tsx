import { Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { Card } from '@/shared/components/Card';

export interface SummaryCardProps {
  title: string;
  value: number;
  caption: string;
  icon: keyof typeof Ionicons.glyphMap;
  colorClass: string;
}

export function SummaryCard({ title, value, caption, icon, colorClass }: SummaryCardProps) {
  return (
    <Card className="flex-1">
      <Ionicons color={colorClass} name={icon} size={32} />
      <Text className="mt-3 text-sm font-semibold text-content-secondary">{title}</Text>
      <Text className="mt-1 text-3xl font-bold" style={{ color: colorClass }}>
        {value}
      </Text>
      <Text className="text-xs text-content-secondary">{caption}</Text>
    </Card>
  );
}
