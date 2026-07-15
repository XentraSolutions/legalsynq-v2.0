import { useCallback, useEffect, useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Pressable, Text, View } from 'react-native';
import { zodResolver } from '@hookform/resolvers/zod';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';

import { AuthHeader } from '@/features/authentication/components';
import { useLogin } from '@/features/authentication/hooks';
import type { AuthStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { Divider } from '@/shared/components/Divider';
import { Input } from '@/shared/components/Input';
import { useToast } from '@/shared/hooks';
import { DeviceSecurityService } from '@/shared/services/DeviceSecurity';
import { TenantSelectionService } from '@/shared/services/TenantSelection';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import type { RememberedTenant } from '@/shared/types/tenant';
import { loginSchema, returningLoginSchema } from '@/shared/validation/authSchemas';

interface LoginScreenFormValues {
  email: string;
  password: string;
  tenantCode?: string;
}

export function LoginScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<AuthStackParamList>>();
  const login = useLogin();
  const toast = useToast();
  const [biometricsAvailable, setBiometricsAvailable] = useState(false);
  const [activeTenant, setActiveTenant] = useState<RememberedTenant | null>(null);
  const [tenantLoading, setTenantLoading] = useState(true);
  const resolver = useMemo(
    () => zodResolver(activeTenant ? returningLoginSchema : loginSchema),
    [activeTenant]
  );
  const {
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginScreenFormValues>({
    defaultValues: {
      email: 'ralph.lopez+1@xentragroup.com',
      password: 'Password@123',
      tenantCode: 'rl-liens1',
    },
    resolver,
  });

  useEffect(() => {
    DeviceSecurityService.isBiometricAvailable()
      .then(setBiometricsAvailable)
      .catch(() => {
        setBiometricsAvailable(false);
      });
  }, []);

  useFocusEffect(
    useCallback(() => {
      let mounted = true;
      setTenantLoading(true);

      TenantSelectionService.getActiveTenant()
        .then((tenant) => {
          if (mounted) {
            setActiveTenant(tenant);
          }
        })
        .catch(() => {
          if (mounted) {
            setActiveTenant(null);
          }
        })
        .finally(() => {
          if (mounted) {
            setTenantLoading(false);
          }
        });

      return () => {
        mounted = false;
      };
    }, [])
  );

  async function submit(values: LoginScreenFormValues) {
    try {
      await login.mutateAsync({
        ...values,
        activeTenant,
      });
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to sign in');
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] px-5 pt-20 dark:bg-[#050506]">
      <AuthHeader subtitle="Sign in to your account" title="Welcome back" />
      {tenantLoading ? (
        <Text
          className={cx(FIGMA_TEXT.body, 'mt-8 text-center text-[#6f737d] dark:text-[#a1a1aa]')}
        >
          Loading tenant preferences...
        </Text>
      ) : (
        <>
          {activeTenant ? (
            <View className="mt-8 rounded-[18px] border border-[#ececee] bg-white p-4 dark:border-[#303138] dark:bg-[#191a1f]">
              <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#6f737d] dark:text-[#a1a1aa]')}>
                Current Tenant
              </Text>
              <Text className="mt-1 font-jakarta-bold text-[20px] leading-[26px] text-[#202228] dark:text-white">
                {activeTenant.tenantName}
              </Text>
              {!activeTenant.isConfirmed ? (
                <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-[#8f929b]')}>
                  This tenant will be confirmed after your next successful sign in.
                </Text>
              ) : null}
              <Pressable
                accessibilityRole="button"
                className="mt-3 self-start"
                onPress={() => navigation.navigate('TenantSelection')}
              >
                <Text className={cx(FIGMA_TEXT.rowValue, 'text-[#f97332]')}>Switch Tenant</Text>
              </Pressable>
            </View>
          ) : null}
          <Controller
            control={control}
            name="email"
            render={({ field: { onChange, value } }) => (
              <Input
                autoCapitalize="none"
                className={activeTenant ? 'mt-5' : 'mt-8'}
                errorMessage={errors.email?.message}
                keyboardType="email-address"
                label="Email address"
                placeholder="name@example.com"
                value={value}
                onChangeText={onChange}
              />
            )}
          />
          {!activeTenant ? (
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
          ) : null}
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
          <Button
            className="mt-6"
            label="Sign In"
            loading={login.isPending}
            onPress={handleSubmit(submit)}
          />

          {biometricsAvailable ? (
            <>
              <Divider label="or continue with" />
              <Button label="Face ID / Touch ID" variant="ghost" />
            </>
          ) : null}
          <Text
            className={cx(FIGMA_TEXT.body, 'mt-8 text-center text-[#6f737d] dark:text-[#a1a1aa]')}
          >
            Do not have an account? <Text className="text-[#f97332]">Contact your admin</Text>
          </Text>
        </>
      )}
    </View>
  );
}
