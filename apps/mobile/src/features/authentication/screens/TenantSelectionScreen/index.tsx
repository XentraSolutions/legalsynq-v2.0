import { useCallback, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { zodResolver } from '@hookform/resolvers/zod';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';

import type { AuthStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { useToast } from '@/shared/hooks';
import { AuthenticationService } from '@/shared/services/Authentication';
import { TenantSelectionService } from '@/shared/services/TenantSelection';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import type { RememberedTenant } from '@/shared/types/tenant';
import { tenantCodeSchema, type TenantCodeFormValues } from '@/shared/validation/authSchemas';

export function TenantSelectionScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<AuthStackParamList>>();
  const toast = useToast();
  const [tenants, setTenants] = useState<RememberedTenant[]>([]);
  const [activeTenant, setActiveTenant] = useState<RememberedTenant | null>(null);
  const [loadingTenantId, setLoadingTenantId] = useState<string | null>(null);
  const [addingTenant, setAddingTenant] = useState(false);
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TenantCodeFormValues>({
    defaultValues: { tenantCode: '' },
    resolver: zodResolver(tenantCodeSchema),
  });

  const loadTenants = useCallback(async () => {
    const [rememberedTenants, currentTenant] = await Promise.all([
      TenantSelectionService.getRememberedTenants(),
      TenantSelectionService.getActiveTenant(),
    ]);
    setTenants(rememberedTenants);
    setActiveTenant(currentTenant);
  }, []);

  useFocusEffect(
    useCallback(() => {
      loadTenants().catch(() => {
        setTenants([]);
        setActiveTenant(null);
      });
    }, [loadTenants])
  );

  async function selectTenant(tenant: RememberedTenant) {
    setLoadingTenantId(tenant.id);
    try {
      await AuthenticationService.clearSession();
      await TenantSelectionService.setActiveTenant(tenant.id);
      navigation.navigate('Login');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to switch tenant');
    } finally {
      setLoadingTenantId(null);
    }
  }

  async function addTenant(values: TenantCodeFormValues) {
    setAddingTenant(true);
    try {
      await AuthenticationService.clearSession();
      await TenantSelectionService.addLocalTenantCode(values.tenantCode);
      reset({ tenantCode: '' });
      navigation.navigate('Login');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to add tenant');
    } finally {
      setAddingTenant(false);
    }
  }

  async function removeTenant(tenant: RememberedTenant) {
    if (tenants.length <= 1) {
      toast.showWarning('At least one tenant code must remain.');
      return;
    }

    if (tenant.id === activeTenant?.id) {
      toast.showWarning('Switch to another tenant before removing this one.');
      return;
    }

    setLoadingTenantId(tenant.id);
    try {
      const removed = await TenantSelectionService.removeRememberedTenant(tenant.id);
      if (!removed) {
        toast.showWarning('This tenant code cannot be removed right now.');
        return;
      }

      await loadTenants();
      toast.showSuccess('Tenant code removed.');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to remove tenant');
    } finally {
      setLoadingTenantId(null);
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Select Tenant" onBack={() => navigation.navigate('Login')} />
      <ScrollView className="flex-1" contentContainerClassName="px-5 pb-10 pt-6">
        <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>
          Choose a tenant stored on this device or add a new tenant code.
        </Text>

        <View className="mt-5 gap-3">
          {tenants.length ? (
            tenants.map((tenant) => {
              const selected = tenant.id === activeTenant?.id;
              const canRemove = tenants.length > 1 && !selected;
              return (
                <View
                  key={tenant.id}
                  className={cx(
                    'rounded-[18px] border bg-white p-4 dark:bg-[#191a1f]',
                    selected ? 'border-[#f97332]' : 'border-[#ececee] dark:border-[#303138]'
                  )}
                >
                  <View className="flex-row items-start justify-between gap-4">
                    <Pressable
                      accessibilityRole="button"
                      className="flex-1 active:opacity-90"
                      disabled={Boolean(loadingTenantId)}
                      onPress={() => selectTenant(tenant)}
                    >
                      <View>
                        <Text className="font-jakarta-bold text-[16px] leading-[22px] text-[#202228] dark:text-white">
                          {tenant.tenantName}
                        </Text>
                        <Text
                          className={cx(
                            FIGMA_TEXT.formLabel,
                            'mt-1 text-[#6f737d] dark:text-[#a1a1aa]'
                          )}
                        >
                          {tenant.tenantCode}
                        </Text>
                        {!tenant.isConfirmed ? (
                          <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-[#8f929b]')}>
                            Pending confirmation on next sign in
                          </Text>
                        ) : null}
                      </View>
                    </Pressable>
                    {selected ? (
                      <Ionicons color="#f97332" name="checkmark-circle" size={24} />
                    ) : canRemove ? (
                      <Pressable
                        accessibilityLabel={`Remove ${tenant.tenantCode}`}
                        accessibilityRole="button"
                        className="h-9 w-9 items-center justify-center rounded-full bg-[#f4f4f5] active:opacity-80 dark:bg-[#2a2b30]"
                        disabled={loadingTenantId === tenant.id}
                        onPress={() => removeTenant(tenant)}
                      >
                        <Ionicons color="#ef4444" name="trash-outline" size={18} />
                      </Pressable>
                    ) : null}
                  </View>
                </View>
              );
            })
          ) : (
            <View className="rounded-[18px] border border-[#ececee] bg-white p-4 dark:border-[#303138] dark:bg-[#191a1f]">
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>
                No saved tenants yet
              </Text>
              <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
                Add a tenant code below to use it on the login screen.
              </Text>
            </View>
          )}
        </View>

        <View className="mt-8 rounded-[18px] border border-[#ececee] bg-white p-4 dark:border-[#303138] dark:bg-[#191a1f]">
          <Text className={cx(FIGMA_TEXT.cardTitle, 'text-[#202228] dark:text-white')}>
            Add New Tenant
          </Text>
          <Controller
            control={control}
            name="tenantCode"
            render={({ field: { onChange, value } }) => (
              <Input
                autoCapitalize='none'
                className="mt-4"
                errorMessage={errors.tenantCode?.message}
                label="Tenant code"
                placeholder="e.g. SMITHLAW"
                value={value}
                onChangeText={onChange}
              />
            )}
          />
          <Button
            className="mt-5"
            label="Continue"
            loading={addingTenant}
            onPress={handleSubmit(addTenant)}
          />
        </View>
      </ScrollView>
    </View>
  );
}
