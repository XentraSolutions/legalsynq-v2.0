import { useState } from 'react';
import { ActivityIndicator, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';
import { useMonthlyAgingReport } from '@/features/dashboard/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { MonthlyAgingReportResponse } from '@/shared/api/endpoints/Liens';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { CardShell } from '../DashboardScreen/CardShell';
import { HeaderIconButton } from '../DashboardReportDetailScreen/HeaderIconButton';
import { PaginationRow } from '../DashboardReportDetailScreen/PaginationRow';
import { formatSellingCurrency } from '../DashboardScreen/selling/sellingDashboardFormatters';

type DetailRoute = RouteProp<MainStackParamList, 'SellingAgingReportDetail'>;
const PAGE_SIZE = 10;

export function getMonthlyAgingRows(report?: Pick<MonthlyAgingReportResponse, 'data'>) {
  return report?.data ?? [];
}

export function SellingAgingReportDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailRoute>();
  const { colorScheme } = useNativeWindColorScheme();
  const isDark = colorScheme === 'dark';
  const [page, setPage] = useState(1);
  const query = useMonthlyAgingReport(route.params.asOfDate, page, PAGE_SIZE);
  const report = query.data;
  const rows = getMonthlyAgingRows(report);
  const currency = report?.currency ?? 'USD';
  const totalPages = Math.max(1, report?.totalPages ?? 1);
  const totalCount = report?.totalCount ?? 0;

  return (
    <SafeAreaView edges={['top']} className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <ScrollView className="flex-1 px-4" contentContainerStyle={{ paddingBottom: 28 }}>
        <View className="mt-3 flex-row items-center gap-3">
          <HeaderIconButton icon="arrow-back-outline" isDark={isDark} onPress={navigation.goBack} />
          <View>
            <Text className={cx(TYPE.screenTitle, 'text-[#24272d] dark:text-white')}>
              A/R Aging Details
            </Text>
            <Text className={cx(TYPE.rowMeta, 'mt-1 text-[#767a84] dark:text-[#a3a4ab]')}>
              As of {route.params.asOfDate}
            </Text>
          </View>
        </View>

        {query.isFetching && !report ? (
          <ActivityIndicator className="mt-12" />
        ) : query.isError ? (
          <Text className="mt-12 text-center text-[#de4b54]">Unable to load aging details.</Text>
        ) : (
          <>
            {rows.map((row) => (
              <CardShell isDark={isDark} key={row.lienCode}>
                <Text className={cx(TYPE.cardTitle, 'text-[#24272d] dark:text-white')}>
                  {row.lienCode}
                </Text>
                <Text className={cx(TYPE.rowMeta, 'mt-1 text-[#767a84] dark:text-[#a3a4ab]')}>
                  {row.fundingCompany}
                </Text>
                <View className="mt-4 gap-2">
                  {[
                    ['Days 1–30', row.days1To30],
                    ['Days 31–60', row.days31To60],
                    ['Days 61–90', row.days61To90],
                    ['Days 91–120', row.days91To120],
                    ['Days 121+', row.moreThan120],
                  ]
                    .filter(([, amount]) => Number(amount) > 0)
                    .map(([label, amount]) => (
                      <View className="flex-row justify-between" key={String(label)}>
                        <Text className={cx(TYPE.rowMeta, 'text-[#767a84] dark:text-[#a3a4ab]')}>
                          {label}
                        </Text>
                        <Text className={cx(TYPE.rowLabel, 'text-[#24272d] dark:text-white')}>
                          {formatSellingCurrency(Number(amount), currency)}
                        </Text>
                      </View>
                    ))}
                </View>
                <View className="mt-4 flex-row justify-between border-t border-[#ececf0] pt-3 dark:border-[#292a2f]">
                  <Text className={cx(TYPE.rowLabel, 'text-[#24272d] dark:text-white')}>Total</Text>
                  <Text className={cx(TYPE.rowLabel, 'text-[#24272d] dark:text-white')}>
                    {formatSellingCurrency(row.totalAmount, currency)}
                  </Text>
                </View>
              </CardShell>
            ))}
            {report && rows.length === 0 ? (
              <Text className="mt-12 text-center text-[#767a84] dark:text-[#a3a4ab]">
                No aging details are available for this date.
              </Text>
            ) : null}
            {report && totalCount > 0 ? (
              <View className="mt-5">
                <PaginationRow
                  canGoNext={page < totalPages}
                  canGoPrevious={page > 1}
                  pagination={{
                    page,
                    pageSize: PAGE_SIZE,
                    totalCount,
                    totalPages,
                  }}
                  onGoToPage={setPage}
                  onNext={() => setPage((current) => Math.min(totalPages, current + 1))}
                  onPrevious={() => setPage((current) => Math.max(1, current - 1))}
                />
              </View>
            ) : null}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
