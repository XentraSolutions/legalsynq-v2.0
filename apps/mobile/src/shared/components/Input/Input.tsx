import type { ReactNode } from 'react';
import { useState } from 'react';
import { Text, TextInput, View, type TextInputProps } from 'react-native';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface InputProps extends TextInputProps {
  label?: string;
  errorMessage?: string;
  hint?: string;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
}

export function Input({
  label,
  errorMessage,
  hint,
  leftIcon,
  rightIcon,
  multiline,
  className,
  ...textInputProps
}: InputProps) {
  const [focused, setFocused] = useState(false);

  return (
    <View className={className}>
      {label ? <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#6f737d] dark:text-[#a1a1aa]')}>{label}</Text> : null}
      <View
        className={cx(
          'flex-row items-center rounded-[14px] border bg-white px-4 dark:bg-[#191a1f]',
          multiline ? 'min-h-[104px] py-3' : 'h-[52px]',
          errorMessage
            ? 'border-error-600'
            : focused
              ? 'border-[#f97332]'
              : 'border-border dark:border-[#303138]'
        )}
      >
        {leftIcon ? <View className="mr-2">{leftIcon}</View> : null}
        <TextInput
          className={cx(FIGMA_TEXT.input, 'flex-1 text-[#202228] dark:text-white')}
          multiline={multiline}
          placeholderTextColor="#94a3b8"
          textAlignVertical={multiline ? 'top' : 'center'}
          onBlur={(event) => {
            setFocused(false);
            textInputProps.onBlur?.(event);
          }}
          onFocus={(event) => {
            setFocused(true);
            textInputProps.onFocus?.(event);
          }}
          {...textInputProps}
        />
        {rightIcon ? <View className="ml-2">{rightIcon}</View> : null}
      </View>
      {errorMessage ? (
        <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-error-600')}>{errorMessage}</Text>
      ) : hint ? (
        <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-content-tertiary dark:text-[#8f929b]')}>{hint}</Text>
      ) : null}
    </View>
  );
}
