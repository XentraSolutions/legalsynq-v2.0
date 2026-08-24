import { MaterialCommunityIcons } from '@expo/vector-icons';
import { Text, View } from 'react-native';

import { Button } from '@/shared/components/Button';
import type { BiometricLabel } from '@/shared/services/DeviceSecurity';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface BiometricLoginButtonProps {
  accountLabel?: string;
  label: BiometricLabel;
  loading?: boolean;
  onPress: () => void;
}

export function BiometricLoginButton({
  accountLabel,
  label,
  loading = false,
  onPress,
}: BiometricLoginButtonProps) {
  const iconName = label === 'Face ID' ? 'face-recognition' : 'fingerprint';

  return (
    <View>
      <Button
        accessibilityLabel={`Sign in with ${label}`}
        label={`Sign in with ${label}`}
        leftIcon={
          <MaterialCommunityIcons
            color="#f97332"
            name={iconName}
            size={22}
            testID="biometric-login-icon"
          />
        }
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
