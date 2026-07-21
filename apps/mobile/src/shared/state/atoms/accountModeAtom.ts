import { atom } from 'jotai';

export type AccountMode = 'selling' | 'buying';

export const accountModeAtom = atom<AccountMode>('buying');
