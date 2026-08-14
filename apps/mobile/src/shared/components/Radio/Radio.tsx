import { Pressable, Text, View } from 'react-native';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface RadioProps {
  selected: boolean;
  onChange: (selected: boolean) => void;
  label?: string;
  disabled?: boolean;
}

export function Radio({ selected, onChange, label, disabled = false }: RadioProps) {
  return (
    <Pressable
      accessibilityRole="radio"
      accessibilityState={{ checked: selected, disabled }}
      disabled={disabled}
      className="flex-row items-center gap-2"
      onPress={() => onChange(true)}
    >
      <View
        className={[
          'h-5 w-5 items-center justify-center rounded-full border',
          selected ? 'border-[#f97332]' : 'border-border dark:border-[#303138]',
          disabled ? 'opacity-50' : '',
        ].join(' ')}
      >
        {selected ? <View className="h-2.5 w-2.5 rounded-full bg-[#f97332]" /> : null}
      </View>
      {label ? <Text className={cx(FIGMA_TEXT.body, 'text-[#202228] dark:text-white')}>{label}</Text> : null}
    </Pressable>
  );
}
