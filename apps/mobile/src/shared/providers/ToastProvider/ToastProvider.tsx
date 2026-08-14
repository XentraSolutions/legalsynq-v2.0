import type { ReactNode } from 'react';
import { useEffect } from 'react';
import { View } from 'react-native';
import { useAtom } from 'jotai';

import { Toast } from '@/shared/components/Toast';
import { toastAtom } from '@/shared/state/atoms/toastAtom';

export interface ToastProviderProps {
  children: ReactNode;
}

export function ToastProvider({ children }: ToastProviderProps) {
  const [toast, setToast] = useAtom(toastAtom);

  useEffect(() => {
    if (!toast.visible) {
      return;
    }

    const timeout = setTimeout(() => {
      setToast((current) => ({ ...current, visible: false }));
    }, 3000);

    return () => clearTimeout(timeout);
  }, [setToast, toast.visible]);

  return (
    <View className="flex-1">
      {children}
      {toast.visible ? <Toast message={toast.message} type={toast.type} /> : null}
    </View>
  );
}
