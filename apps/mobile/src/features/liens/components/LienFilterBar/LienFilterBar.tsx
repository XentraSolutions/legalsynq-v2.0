import { ScrollView, View } from 'react-native';

import { Chip } from '@/shared/components/Chip';
import type { LienFilter } from '@/features/liens/types/types';

export const LIEN_FILTERS: LienFilter[] = [
  { id: 'all', label: 'All' },
  { id: 'auto', label: 'Auto Accident', caseType: 'AUTO_ACCIDENT' },
  { id: 'workers', label: 'Workers Comp', caseType: 'WORKERS_COMP' },
  { id: 'injury', label: 'Personal Injury', caseType: 'PERSONAL_INJURY' },
  { id: 'medical', label: 'Medical', caseType: 'MEDICAL_MALPRACTICE' },
  { id: 'lt-50', label: '< $50K', maxAmount: 50000 },
  { id: '50-200', label: '$50K-$200K', minAmount: 50000, maxAmount: 200000 },
  { id: 'gt-200', label: '> $200K', minAmount: 200000 },
];

export interface LienFilterBarProps {
  activeFilter: LienFilter;
  onFilterChange: (filter: LienFilter) => void;
}

export function LienFilterBar({ activeFilter, onFilterChange }: LienFilterBarProps) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false}>
      <View className="flex-row gap-2 px-5 py-3">
        {LIEN_FILTERS.map((filter) => (
          <Chip
            key={filter.id}
            label={filter.label}
            selected={filter.id === activeFilter.id}
            onPress={() => onFilterChange(filter)}
          />
        ))}
      </View>
    </ScrollView>
  );
}
