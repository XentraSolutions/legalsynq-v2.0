import { useState } from 'react';
import { Text, View } from 'react-native';
import { useAtom } from 'jotai';

import { Button } from '@/shared/components/Button';
import { Modal } from '@/shared/components/Modal';
import { useToast } from '@/shared/hooks';
import {
  BiometricAuthenticationCancelledError,
  BiometricAuthenticationService,
} from '@/shared/services/Authentication';
import { biometricEnrollmentOfferAtom } from '@/shared/state/atoms/biometricAtom';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export function BiometricEnrollmentModal() {
  const [offer, setOffer] = useAtom(biometricEnrollmentOfferAtom);
  const [isEnabling, setIsEnabling] = useState(false);
  const toast = useToast();

  function dismiss(): void {
    setOffer((current) => ({ ...current, visible: false }));
  }

  async function enable(): Promise<void> {
    setIsEnabling(true);
    try {
      await BiometricAuthenticationService.enable();
      dismiss();
      toast.showSuccess('Biometric login has been enabled on this device.');
    } catch (error) {
      if (!(error instanceof BiometricAuthenticationCancelledError)) {
        toast.showError(
          error instanceof Error ? error.message : 'Unable to enable biometric login.'
        );
      }
    } finally {
      setIsEnabling(false);
    }
  }

  return (
    <Modal
      footer={
        <View className="gap-3">
          <Button label="Enable" loading={isEnabling} onPress={() => void enable()} />
          <Button disabled={isEnabling} label="Not Now" variant="ghost" onPress={dismiss} />
        </View>
      }
      title="Enable Biometric Login"
      visible={offer.visible}
      onClose={dismiss}
    >
      <Text className={cx(FIGMA_TEXT.body, 'text-[#555964] dark:text-[#d8d9dd]')}>
        Use {offer.label} or your device authentication to sign in faster on this device. Your
        biometric information stays on your device and is not shared with LegalSynq.
      </Text>
      <Text className={cx(FIGMA_TEXT.formLabel, 'mt-3 text-[#8d9098] dark:text-[#8f929b]')}>
        You can disable biometric login at any time. Your password may still be required in some
        circumstances.
      </Text>
    </Modal>
  );
}
