import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Pressable, Text, View } from 'react-native';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';

import { AuthHeader } from '@/features/authentication/components';
import { useLogin } from '@/features/authentication/hooks';
import type { AuthStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { Divider } from '@/shared/components/Divider';
import { Input } from '@/shared/components/Input';
import { useToast } from '@/shared/hooks';
import { DeviceSecurityService } from '@/shared/services/DeviceSecurity';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { loginSchema, type LoginFormValues } from '@/shared/validation/authSchemas';

export function LoginScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<AuthStackParamList>>();
  const login = useLogin();
  const toast = useToast();
  const [biometricsAvailable, setBiometricsAvailable] = useState(false);
  const {
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    defaultValues: {
      email: 'ralph.lopez+1@xentragroup.com',
      password: 'Password@123',
      tenantCode: 'rl-liens1',
    },
    resolver: zodResolver(loginSchema),
  });

  useEffect(() => {
    DeviceSecurityService.isBiometricAvailable().then(setBiometricsAvailable).catch(() => {
      setBiometricsAvailable(false);
    });
  }, []);

  async function submit(values: LoginFormValues) {
    try {
      await login.mutateAsync(values);
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to sign in');
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] px-5 pt-20 dark:bg-[#050506]">
      <AuthHeader subtitle="Sign in to your account" title="Welcome back" />
      <Controller
        control={control}
        name="email"
        render={({ field: { onChange, value } }) => (
          <Input
            autoCapitalize="none"
            className="mt-8"
            errorMessage={errors.email?.message}
            keyboardType="email-address"
            label="Email address"
            placeholder="name@example.com"
            value={value}
            onChangeText={onChange}
          />
        )}
      />
      <Controller
        control={control}
        name="tenantCode"
        render={({ field: { onChange, value } }) => (
          <Input
            autoCapitalize="characters"
            className="mt-4"
            errorMessage={errors.tenantCode?.message}
            label="Tenant code"
            placeholder="e.g. SMITHLAW"
            value={value}
            onChangeText={onChange}
          />
        )}
      />
      <Controller
        control={control}
        name="password"
        render={({ field: { onChange, value } }) => (
          <Input
            className="mt-4"
            errorMessage={errors.password?.message}
            label="Password"
            placeholder="Password"
            secureTextEntry
            value={value}
            onChangeText={onChange}
          />
        )}
      />
      <Pressable
        accessibilityRole="button"
        className="mt-2 items-end"
        onPress={() => navigation.navigate('ForgotPassword')}
      >
        <Text className={cx(FIGMA_TEXT.rowValue, 'text-[#f97332]')}>Forgot Password?</Text>
      </Pressable>
      <Button className="mt-6" label="Sign In" loading={login.isPending} onPress={handleSubmit(submit)} />
      
      {biometricsAvailable ? <><Divider label="or continue with" /><Button label="Face ID / Touch ID" variant="ghost" /></> : null}
      <Text className={cx(FIGMA_TEXT.body, 'mt-8 text-center text-[#6f737d] dark:text-[#a1a1aa]')}>
        Do not have an account? <Text className="text-[#f97332]">Contact your admin</Text>
      </Text>
    </View>
  );
}
