import { createNativeStackNavigator } from '@react-navigation/native-stack';

import { ForgotPasswordScreen, LoginScreen } from '@/features/authentication';
import type { AuthStackParamList } from '@/navigation/types/navigation';

const Stack = createNativeStackNavigator<AuthStackParamList>();

export function AuthStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen component={LoginScreen} name="Login" />
      <Stack.Screen component={ForgotPasswordScreen} name="ForgotPassword" />
    </Stack.Navigator>
  );
}
