import { createNavigationContainerRef } from '@react-navigation/native';

import type { RootStackParamList } from '@/navigation/types/navigation';

export const rootNavigationRef = createNavigationContainerRef<RootStackParamList>();
