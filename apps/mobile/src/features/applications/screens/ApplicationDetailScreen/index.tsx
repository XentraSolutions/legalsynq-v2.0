import { ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp, RouteProp } from '@react-navigation/native';

import { useApplicationDetail } from '@/features/applications/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Card, Divider, EmptyState, Header, Spinner } from '@/shared/components';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { ApiError } from '@/shared/types/api';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

function displayValue(value: string | null | undefined): string {
  return value?.trim() || '—';
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <View className="flex-row justify-between gap-4 py-2">
      <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>{label}</Text>
      <Text
        className={cx(FIGMA_TEXT.bodyStrong, 'flex-1 text-right text-[#202228] dark:text-white')}
      >
        {value}
      </Text>
    </View>
  );
}

export function ApplicationDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<RouteProp<MainStackParamList, 'ApplicationDetail'>>();
  const query = useApplicationDetail(route.params.applicationId);
  const application = query.data;
  const isNotFound = query.error instanceof ApiError && query.error.statusCode === 404;

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header
        showBack
        title={application?.applicationNumber || 'Application Details'}
        subtitle={application ? `Status: ${application.status}` : undefined}
        onBack={() => navigation.goBack()}
      />
      {query.isLoading ? (
        <View
          accessibilityLabel="Loading application"
          className="flex-1 items-center justify-center"
        >
          <Spinner />
        </View>
      ) : query.isError || !application ? (
        <EmptyState
          title="Application unavailable"
          description="This application could not be loaded. It may not exist or you may not have access."
          actionLabel={query.isError && !isNotFound ? 'Try Again' : 'Go Back'}
          icon={<Ionicons color="#ee7132" name="document-text-outline" size={58} />}
          onAction={() => (query.isError && !isNotFound ? query.refetch() : navigation.goBack())}
        />
      ) : (
        <ScrollView contentContainerClassName="gap-4 px-5 pb-10 pt-4">
          <Card>
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              Funding Request
            </Text>
            <Divider />
            <DetailRow label="Application Number" value={application.applicationNumber} />
            <DetailRow label="Status" value={application.status} />
            <DetailRow
              label="Requested Amount"
              value={
                application.requestedAmount === null
                  ? '—'
                  : formatCurrency(application.requestedAmount)
              }
            />
            {application.approvedAmount !== null ? (
              <DetailRow
                label="Approved Amount"
                value={formatCurrency(application.approvedAmount)}
              />
            ) : null}
            <DetailRow label="Case Type" value={displayValue(application.caseType)} />
            <DetailRow
              label="Incident Date"
              value={application.incidentDate ? formatDisplayDate(application.incidentDate) : '—'}
            />
          </Card>

          <Card>
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              Applicant
            </Text>
            <Divider />
            <DetailRow
              label="Name"
              value={`${application.applicantFirstName} ${application.applicantLastName}`.trim()}
            />
            <DetailRow label="Email" value={application.email} />
            <DetailRow label="Phone" value={application.phone} />
          </Card>

          <Card>
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              Timeline
            </Text>
            <Divider />
            <DetailRow label="Created" value={formatDisplayDate(application.createdAtUtc)} />
            <DetailRow label="Last Updated" value={formatDisplayDate(application.updatedAtUtc)} />
          </Card>
        </ScrollView>
      )}
    </View>
  );
}
