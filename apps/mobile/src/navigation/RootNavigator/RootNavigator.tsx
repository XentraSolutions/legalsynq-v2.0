import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useAtomValue } from 'jotai';

import { AuthStack } from '@/navigation/AuthStack';
import { MainStack } from '@/navigation/MainStack';
import type { RootStackParamList } from '@/navigation/types/navigation';
import { authAtom } from '@/shared/state/atoms/authAtom';

const Stack = createNativeStackNavigator<RootStackParamList>();

export function RootNavigator() {
  const { isAuthenticated } = useAtomValue(authAtom);

  return (
    <NavigationContainer>
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {isAuthenticated ? (
          <Stack.Screen component={MainStack} name="Main" />
        ) : (
          <Stack.Screen component={AuthStack} name="Auth" />
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
}
