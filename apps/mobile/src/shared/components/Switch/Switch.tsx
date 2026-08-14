import { Switch as RNSwitch, View } from 'react-native';

import { COLORS } from '@/shared/styles/tokens';

export interface SwitchProps {
  value: boolean;
  onValueChange: (value: boolean) => void;
  disabled?: boolean;
}

export function Switch({ value, onValueChange, disabled = false }: SwitchProps) {
  return (
    <View className={disabled ? 'opacity-50' : undefined}>
      <RNSwitch
        disabled={disabled}
        ios_backgroundColor="#cbd5e1"
        thumbColor="#ffffff"
        trackColor={{ false: '#cbd5e1', true: COLORS.primary }}
        value={value}
        onValueChange={onValueChange}
      />
    </View>
  );
}
