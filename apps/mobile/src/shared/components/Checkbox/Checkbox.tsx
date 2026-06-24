import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface CheckboxProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label?: string;
  disabled?: boolean;
}

export function Checkbox({ checked, onChange, label, disabled = false }: CheckboxProps) {
  return (
    <Pressable
      accessibilityRole="checkbox"
      accessibilityState={{ checked, disabled }}
      disabled={disabled}
      className="flex-row items-center gap-2"
      onPress={() => onChange(!checked)}
    >
      <View
        className={[
          'h-5 w-5 items-center justify-center rounded border',
          checked ? 'border-[#f97332] bg-[#f97332]' : 'border-border bg-white dark:border-[#303138] dark:bg-[#191a1f]',
          disabled ? 'opacity-50' : '',
        ].join(' ')}
      >
        {checked ? <Ionicons color="#ffffff" name="checkmark" size={14} /> : null}
      </View>
      {label ? <Text className={cx(FIGMA_TEXT.body, 'text-[#202228] dark:text-white')}>{label}</Text> : null}
    </Pressable>
  );
}
