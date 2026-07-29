import { atom } from 'jotai';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface ToastState {
  visible: boolean;
  message: string;
  type: ToastType;
}

export const toastAtom = atom<ToastState>({
  visible: false,
  message: '',
  type: 'info',
});
