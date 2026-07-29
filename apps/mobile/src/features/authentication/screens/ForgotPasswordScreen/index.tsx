import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';

import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import type { AuthStackParamList } from '@/navigation/types/navigation';
import { AuthenticationService } from '@/shared/services/Authentication';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { forgotPasswordSchema, type ForgotPasswordFormValues } from '@/shared/validation/authSchemas';

export function ForgotPasswordScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<AuthStackParamList>>();
  const [success, setSuccess] = useState(false);
  const [loading, setLoading] = useState(false);
  const {
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>({
    defaultValues: { email: '' },
    resolver: zodResolver(forgotPasswordSchema),
  });

  async function submit(values: ForgotPasswordFormValues) {
    setLoading(true);
    try {
      await AuthenticationService.forgotPassword(values);
    } catch {
      // Development prototypes still show the success path when the gateway is offline.
    } finally {
      setLoading(false);
      setSuccess(true);
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Forgot Password" onBack={() => navigation.goBack()} />
      {success ? (
        <EmptyState
          actionLabel="Back to Sign In"
          description="We sent reset instructions if an account exists for that email."
          icon={<Ionicons color="#16a34a" name="mail-open" size={64} />}
          title="Check your email"
          onAction={() => navigation.goBack()}
        />
      ) : (
      <View className="px-5 pt-8">
        <View className="items-center">
          <Ionicons color="#f97332" name="lock-closed" size={64} />
          <Text className="mt-4 text-center font-jakarta-semibold text-[24px] leading-[30px] text-[#202228] dark:text-white">
            Reset your password
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mx-6 mt-2 text-center text-[#6f737d] dark:text-[#a1a1aa]')}>
            Enter your email and we will send reset instructions
          </Text>
        </View>
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
        <Button className="mt-6" label="Send Reset Link" loading={loading} onPress={handleSubmit(submit)} />
        <Button className="mt-3" label="Back to Sign In" variant="ghost" onPress={() => navigation.goBack()} />
      </View>
      )}
    </View>
  );
}
