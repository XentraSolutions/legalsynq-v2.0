import { ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp, RouteProp } from '@react-navigation/native';
import { useFacility, useFacilityStaff } from '../hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button, Card, EmptyState, Header, Spinner } from '@/shared/components';
import { cx, FIGMA_TEXT } from '@/shared/styles';

export function FacilityDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<RouteProp<MainStackParamList, 'FacilityDetail'>>();
  const query = useFacility(route.params.facilityId);
  const staff = useFacilityStaff(route.params.facilityId);
  const facility = query.data;
  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <Header showBack title="Medical Facility" onBack={() => navigation.goBack()} />
      {query.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      ) : !facility ? (
        <EmptyState
          title="Facility unavailable"
          description="This facility could not be loaded."
          actionLabel="Go Back"
          icon={<Ionicons color="#ee7132" name="medkit-outline" size={58} />}
          onAction={() => navigation.goBack()}
        />
      ) : (
        <ScrollView contentContainerClassName="gap-4 px-6 pb-10 pt-4">
          <Card className="rounded-[20px] p-6">
            <Text className="font-jakarta-semibold text-[20px] text-[#18181b] dark:text-white">
              {facility.name}
            </Text>
            <Detail icon="mail-outline" value={facility.email} />
            <Detail icon="call-outline" value={facility.phone} />
            <Detail icon="print-outline" value={facility.fax} />
            <Detail
              icon="location-outline"
              value={[
                facility.addressLine1,
                facility.addressLine2,
                facility.city,
                facility.state,
                facility.postalCode,
              ]
                .filter(Boolean)
                .join(', ')}
            />
          </Card>
          <View className="flex-row items-center">
            <Text className="flex-1 font-jakarta-semibold text-[16px] text-[#18181b] dark:text-white">
              Medical Facility Staff
            </Text>
          </View>
          {staff.isLoading ? (
            <Spinner />
          ) : (staff.data ?? []).length === 0 ? (
            <Text className={cx(FIGMA_TEXT.body, 'text-[#71717a]')}>No facility staff yet.</Text>
          ) : (
            (staff.data ?? []).map((person) => (
              <Card key={person.id} className="rounded-[20px] p-5">
                <Text className="font-jakarta-semibold text-[15px] text-[#18181b] dark:text-white">
                  {person.firstName} {person.lastName}
                </Text>
                {person.position ? (
                  <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-[#71717a]')}>
                    {person.position}
                  </Text>
                ) : null}
                <Detail icon="mail-outline" value={person.email} />
                <Detail icon="call-outline" value={person.phone} />
              </Card>
            ))
          )}
          <Button
            label="Edit Facility"
            variant="secondary"
            onPress={() => navigation.navigate('FacilityForm', { facilityId: facility.id })}
          />
        </ScrollView>
      )}
    </View>
  );
}
function Detail({ icon, value }: { icon: keyof typeof Ionicons.glyphMap; value?: string | null }) {
  return value ? (
    <View className="mt-3 flex-row items-start gap-2">
      <Ionicons color="#8f929b" name={icon} size={17} />
      <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#71717a]')}>{value}</Text>
    </View>
  ) : null;
}
