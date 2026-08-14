import { useEffect, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { LienManagementFilterModal } from '@/features/liens/components';
import { useCaseManagementLiens } from '@/features/liens/hooks';
import {
  EMPTY_LIEN_MANAGEMENT_FILTERS,
  type LienManagementFilters,
  type ManagementLienListItem,
} from '@/features/liens/types/types';
import type { CaseDetailResponse, CaseUpdate } from '@/shared/api/endpoints/Cases';
import { Badge, type BadgeVariant } from '@/shared/components/Badge';
import { Button } from '@/shared/components/Button';
import { SearchBar } from '@/shared/components/SearchBar';
import { Spinner } from '@/shared/components/Spinner';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

const PAGE_SIZE = 5;

function displayDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return formatDisplayDate(value, 'MM/dd/yyyy');
  } catch {
    return value;
  }
}

function statusVariant(status: string): BadgeVariant {
  const value = status.toLowerCase();
  if (value.includes('closed') || value.includes('rejected')) return 'error';
  if (value.includes('pending')) return 'warning';
  if (value.includes('open') || value.includes('active')) return 'success';
  return 'neutral';
}

function activeFilterCount(filters: LienManagementFilters): number {
  return [
    filters.purchaseStartDate || filters.purchaseEndDate,
    filters.closedStartDate || filters.closedEndDate,
    filters.lawFirmId,
    filters.medicalFacilityId,
    filters.caseManagerId,
    filters.statusId,
  ].filter(Boolean).length;
}

function updateTimestamp(update: CaseUpdate): string {
  const timestamp =
    update.updatedAtUtc ?? update.updatedAt ?? update.createdAtUtc ?? update.createdAt ?? '';
  return displayDate(timestamp);
}

function CollapsibleCard({
  children,
  title,
}: {
  children: React.ReactNode;
  title: string;
}) {
  const [expanded, setExpanded] = useState(true);
  return (
    <View
      className="rounded-[20px] bg-white px-6 pb-5 pt-6 dark:bg-[#191a1f]"
      style={SHADOWS.sm}
    >
      <Pressable
        accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${title}`}
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        className="flex-row items-center gap-2"
        onPress={() => setExpanded((current) => !current)}
      >
        <Ionicons color="#71717a" name={expanded ? 'chevron-down' : 'chevron-forward'} size={18} />
        <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
          {title}
        </Text>
      </Pressable>
      {expanded ? <View className="mt-4">{children}</View> : null}
    </View>
  );
}

function LienValueRow({
  icon,
  label,
  value,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  value: string;
}) {
  return (
    <View className="flex-row items-center gap-2 py-1.5">
      <Ionicons color="#8f929b" name={icon} size={16} />
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#858892] dark:text-[#a1a1aa]')}>
        {label}
      </Text>
      <Text className={cx(FIGMA_TEXT.formLabel, 'max-w-[48%] text-right text-[#202228] dark:text-white')}>
        {value}
      </Text>
    </View>
  );
}

function CaseLienRow({ lien, onView }: { lien: ManagementLienListItem; onView: () => void }) {
  return (
    <View className="border-b border-[#e4e4e7] pb-6 pt-3 dark:border-[#303138]">
      <View className="flex-row items-start gap-3">
        <View className="flex-1">
          <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>
            {lien.medicalFacility || lien.patientName}
          </Text>
          <Text className={cx(FIGMA_TEXT.formLabel, 'mt-1 text-[#858892] dark:text-[#a1a1aa]')}>
            Lien ID: {lien.lienNumber}
          </Text>
        </View>
        <View className="items-end gap-2">
          <Pressable
            accessibilityLabel={`Open lien ${lien.lienNumber}`}
            accessibilityRole="button"
            hitSlop={10}
            onPress={onView}
          >
            <Ionicons color="#777984" name="ellipsis-vertical" size={18} />
          </Pressable>
          <Badge label={lien.status} variant={statusVariant(lien.status)} />
        </View>
      </View>
      <View className="mt-4">
        <LienValueRow icon="calendar-outline" label="Initial Service Date" value={displayDate(lien.initialServiceDate)} />
        <LienValueRow icon="calendar-outline" label="Purchase Date" value={displayDate(lien.purchaseDate)} />
        <LienValueRow icon="cash-outline" label="Purchase Amount" value={formatCurrency(lien.purchaseAmount)} />
        <LienValueRow icon="receipt-outline" label="Billing Amount" value={formatCurrency(lien.billingAmount)} />
      </View>
      <Button className="mt-4" label="View Lien" size="sm" variant="secondary" onPress={onView} />
    </View>
  );
}

function PageButton({
  disabled,
  direction,
  label,
  onPress,
}: {
  disabled: boolean;
  direction: 'back' | 'forward';
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={`${label} page`}
      accessibilityRole="button"
      className={cx(
        'h-9 flex-row items-center gap-1 rounded-full border border-[#e4e4e7] px-3 dark:border-[#303138]',
        disabled && 'opacity-40'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      {direction === 'back' ? <Ionicons color="#71717a" name="chevron-back" size={14} /> : null}
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#202228] dark:text-white')}>{label}</Text>
      {direction === 'forward' ? <Ionicons color="#202228" name="chevron-forward" size={14} /> : null}
    </Pressable>
  );
}

function EmptyLiens({ onCreate }: { onCreate: () => void }) {
  return (
    <View className="items-center px-1 pb-1 pt-3">
      <View className="h-10 w-10 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]">
        <Ionicons color="#202228" name="document-text-outline" size={18} />
      </View>
      <Text className="mt-5 text-center font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
        No Available Case Liens Yet
      </Text>
      <Text className="mt-2 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
        No available case liens. Add your first case lien by tapping ‘Add Liens’ below.
      </Text>
      <Button className="mt-5 w-full" label="+ Add Liens" onPress={onCreate} />
    </View>
  );
}

export function CaseLiensTab({
  caseItem,
  updates,
  updatesLoading,
  onCreate,
  onView,
}: {
  caseItem: CaseDetailResponse;
  updates: CaseUpdate[];
  updatesLoading: boolean;
  onCreate: () => void;
  onView: (lienId: string) => void;
}) {
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
  const [draftFilters, setDraftFilters] = useState({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
  const [filterVisible, setFilterVisible] = useState(false);
  const [page, setPage] = useState(1);
  const [showAllUpdates, setShowAllUpdates] = useState(false);
  const liensQuery = useCaseManagementLiens(caseItem, search, filters);
  const lienUpdates = updates;
  const visibleUpdates = showAllUpdates ? lienUpdates : lienUpdates.slice(0, 3);
  const filterCount = activeFilterCount(filters);
  const pageCount = Math.max(1, Math.ceil(liensQuery.liens.length / PAGE_SIZE));
  const pageLiens = liensQuery.liens.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  useEffect(() => {
    if (page > pageCount) setPage(pageCount);
  }, [page, pageCount]);

  return (
    <>
      <CaseDetailTabPage testID="case-liens-page">
        <View className="mb-6 flex-row items-center gap-3">
          <View className="flex-1">
            <SearchBar
              placeholder="Search..."
              value={search}
              onChangeText={(value) => {
                setSearch(value);
                setPage(1);
              }}
            />
          </View>
          <Pressable
            accessibilityLabel="Filter case liens"
            accessibilityRole="button"
            className="h-11 w-11 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]"
            onPress={() => {
              setDraftFilters({ ...filters });
              setFilterVisible(true);
            }}
          >
            <Ionicons color="#777984" name="options-outline" size={21} />
            {filterCount ? (
              <View className="absolute -right-1 -top-1 min-w-[18px] items-center rounded-full bg-[#f97332] px-1 py-0.5">
                <Text className={cx(FIGMA_TEXT.microMeta, 'text-white')}>{filterCount}</Text>
              </View>
            ) : null}
          </Pressable>
          <Pressable
            accessibilityLabel="Add case lien"
            accessibilityRole="button"
            className="h-11 w-11 items-center justify-center rounded-full bg-[#f97332]"
            onPress={onCreate}
          >
            <Ionicons color="white" name="add" size={24} />
          </Pressable>
        </View>

        {filterCount ? (
          <View className="mb-4 flex-row items-center gap-2">
            <View className="h-1.5 w-1.5 rounded-full bg-[#f97332]" />
            <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#777a84]')}>
              {filterCount} Filter(s) Applied
            </Text>
            <Button
              label="Clear Filter"
              size="sm"
              variant="ghost"
              onPress={() => {
                setFilters({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
                setDraftFilters({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
                setPage(1);
              }}
            />
          </View>
        ) : null}

        <CollapsibleCard title="Liens">
          {liensQuery.isLoading ? (
            <View className="items-center py-12"><Spinner /></View>
          ) : liensQuery.isError ? (
            <View className="items-center py-8">
              <Text className={cx(FIGMA_TEXT.body, 'text-center text-[#777a84]')}>
                The liens for this case could not be loaded.
              </Text>
              <Button className="mt-4" label="Try Again" size="sm" onPress={() => void liensQuery.refetchAll()} />
            </View>
          ) : liensQuery.totalCount === 0 ? (
            <EmptyLiens onCreate={onCreate} />
          ) : liensQuery.liens.length === 0 ? (
            <View className="items-center py-8">
              <Text className={cx(FIGMA_TEXT.body, 'text-center text-[#777a84]')}>No matching case liens.</Text>
              <Button
                className="mt-4"
                label="Clear Filters"
                size="sm"
                variant="secondary"
                onPress={() => {
                  setSearch('');
                  setFilters({ ...EMPTY_LIEN_MANAGEMENT_FILTERS });
                }}
              />
            </View>
          ) : (
            <>
              {pageLiens.map((lien) => (
                <CaseLienRow key={lien.id} lien={lien} onView={() => onView(lien.id)} />
              ))}
              <View className="mt-5 flex-row items-center">
                <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#858892]')}>
                  {Math.min(page * PAGE_SIZE, liensQuery.liens.length)} of {liensQuery.liens.length} entries
                </Text>
                <PageButton
                  direction="back"
                  disabled={page === 1}
                  label="Previous"
                  onPress={() => setPage((current) => Math.max(1, current - 1))}
                />
                <View className="w-2" />
                <PageButton
                  direction="forward"
                  disabled={page === pageCount}
                  label="Next"
                  onPress={() => setPage((current) => Math.min(pageCount, current + 1))}
                />
              </View>
            </>
          )}
        </CollapsibleCard>

        <View className="mt-6">
          <CollapsibleCard title="Recent Updates">
            {updatesLoading ? (
              <View className="items-center py-8"><Spinner /></View>
            ) : visibleUpdates.length ? (
              <>
                {visibleUpdates.map((update, index) => (
                  <View
                    className={cx(
                      'py-4',
                      index < visibleUpdates.length - 1
                        ? 'border-b border-[#e4e4e7] dark:border-[#303138]'
                        : ''
                    )}
                    key={update.id ?? `${updateTimestamp(update)}-${index}`}
                  >
                    <View className="flex-row items-start gap-3">
                      <Text className={cx(FIGMA_TEXT.bodyStrong, 'flex-1 text-[#202228] dark:text-white')}>
                        {update.title ?? update.action ?? 'Lien Update'}
                      </Text>
                      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#858892]')}>
                        {updateTimestamp(update)}
                      </Text>
                    </View>
                    <Text className={cx(FIGMA_TEXT.formLabel, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
                      {update.description ?? update.message ?? update.note ?? 'Lien record updated.'}
                    </Text>
                  </View>
                ))}
                {lienUpdates.length > 3 ? (
                  <Button
                    className="mt-3"
                    label={showAllUpdates ? 'Show Recent Updates' : 'View All Updates →'}
                    size="sm"
                    variant="secondary"
                    onPress={() => setShowAllUpdates((current) => !current)}
                  />
                ) : null}
              </>
            ) : (
              <View className="items-center px-1 py-5">
                <View className="h-10 w-10 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]">
                  <Ionicons color="#202228" name="time-outline" size={18} />
                </View>
                <Text className="mt-5 text-center font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
                  No Recent Updates
                </Text>
                <Text className="mt-2 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
                  No recent lien updates have been recorded yet. They will appear here once available.
                </Text>
              </View>
            )}
          </CollapsibleCard>
        </View>
      </CaseDetailTabPage>

      <LienManagementFilterModal
        draft={draftFilters}
        options={liensQuery.filterOptions}
        visible={filterVisible}
        onApply={() => {
          setFilters({ ...draftFilters });
          setFilterVisible(false);
          setPage(1);
        }}
        onChange={setDraftFilters}
        onClose={() => setFilterVisible(false)}
        onReset={() => setDraftFilters({ ...EMPTY_LIEN_MANAGEMENT_FILTERS })}
      />
    </>
  );
}
