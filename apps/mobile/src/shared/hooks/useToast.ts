import { useCallback } from 'react';
import { useSetAtom } from 'jotai';

import { toastAtom, type ToastType } from '@/shared/state/atoms/toastAtom';

export function useToast() {
  const setToast = useSetAtom(toastAtom);

  const showToast = useCallback(
    (message: string, type: ToastType = 'info') => {
      setToast({ visible: true, message, type });
    },
    [setToast]
  );

  return {
    showToast,
    showSuccess: (message: string) => showToast(message, 'success'),
    showError: (message: string) => showToast(message, 'error'),
    showInfo: (message: string) => showToast(message, 'info'),
    showWarning: (message: string) => showToast(message, 'warning'),
  };
}
