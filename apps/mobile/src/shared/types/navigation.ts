import type { AuthStackParamList, MainStackParamList, RootStackParamList } from '@/navigation/types/navigation';

export type { AuthStackParamList, MainStackParamList, RootStackParamList };

export type MainRouteName = keyof MainStackParamList;
export type AuthRouteName = keyof AuthStackParamList;
export type RootRouteName = keyof RootStackParamList;
