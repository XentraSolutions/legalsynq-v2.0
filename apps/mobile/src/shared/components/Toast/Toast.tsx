import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import type { ToastType } from '@/shared/state/atoms/toastAtom';

export interface ToastProps {
  message: string;
  type?: ToastType;
}

const TOAST_CLASSES: Record<ToastType, string> = {
  success: 'bg-success-600',
  error: 'bg-error-600',
  info: 'bg-info-600',
  warning: 'bg-warning-600',
};

const ICONS: Record<ToastType, keyof typeof Ionicons.glyphMap> = {
  success: 'checkmark-circle',
  error: 'alert-circle',
  info: 'information-circle',
  warning: 'warning',
};

export function Toast({ message, type = 'info' }: ToastProps) {
  return (
    <View
      pointerEvents="none"
      className={`absolute left-0 right-0 top-16 z-50 mx-4 flex-row items-center gap-3 rounded-lg px-4 py-3 shadow-lg ${TOAST_CLASSES[type]}`}
    >
      <Ionicons color="#ffffff" name={ICONS[type]} size={20} />
      <Text className="flex-1 text-base font-medium text-white">{message}</Text>
    </View>
  );
}
