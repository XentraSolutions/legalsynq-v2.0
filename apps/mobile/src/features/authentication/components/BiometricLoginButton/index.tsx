import { Ionicons } from '@expo/vector-icons';
import { Text, View } from 'react-native';

import { Button } from '@/shared/components/Button';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface BiometricLoginButtonProps {
  accountLabel?: string;
  label: string;
  loading?: boolean;
  onPress: () => void;
}

export function BiometricLoginButton({
  accountLabel,
  label,
  loading = false,
  onPress,
}: BiometricLoginButtonProps) {
  return (
    <View>
      <Button
        accessibilityLabel={`Sign in with ${label}`}
        label={`Sign in with ${label}`}
        leftIcon={<Ionicons color="#f97332" name="finger-print-outline" size={22} />}
        loading={loading}
        variant="ghost"
        onPress={onPress}
      />
      {accountLabel ? (
        <Text
          className={cx(
            FIGMA_TEXT.formLabel,
            'mt-1 text-center text-[#8d9098] dark:text-[#8f929b]'
          )}
        >
          Continue as {accountLabel}
        </Text>
      ) : null}
    </View>
  );
}
