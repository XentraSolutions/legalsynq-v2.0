import { Text, View } from 'react-native';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import type { ReportPaginationMeta } from './types';
import { PagePill } from './PagePill';
import { PaginationButton } from './PaginationButton';

function buildPaginationPageList(current: number, total: number): Array<number | 'ellipsis'> {
  const pages = Array.from(new Set([1, current, total])).sort((left, right) => left - right);
  const result: Array<number | 'ellipsis'> = [];

  pages.forEach((page, index) => {
    if (index > 0 && page - pages[index - 1] > 1) result.push('ellipsis');
    result.push(page);
  });

  return result;
}

export function PaginationRow({
  canGoNext,
  canGoPrevious,
  pagination,
  onGoToPage,
  onNext,
  onPrevious,
}: {
  canGoNext: boolean;
  canGoPrevious: boolean;
  pagination: ReportPaginationMeta;
  onGoToPage: (page: number) => void;
  onNext: () => void;
  onPrevious: () => void;
}) {
  const pageList = buildPaginationPageList(pagination.page, pagination.totalPages);

  return (
    <View className="flex-row items-center gap-2">
      <View className="flex-1 flex-row items-center gap-2">
        {pageList.map((entry, index) =>
          entry === 'ellipsis' ? (
            <Text
              className={cx(TYPE.rowMuted, 'text-[#71717a] dark:text-[#a1a1aa]')}
              key={`ellipsis-${index}`}
            >
              ...
            </Text>
          ) : (
            <PagePill
              isCurrent={entry === pagination.page}
              key={entry}
              page={entry}
              onPress={() => onGoToPage(entry)}
            />
          )
        )}
      </View>
      <PaginationButton
        disabled={!canGoPrevious}
        icon="chevron-back-outline"
        label="Previous"
        onPress={onPrevious}
      />
      <PaginationButton
        disabled={!canGoNext}
        icon="chevron-forward-outline"
        label="Next"
        onPress={onNext}
      />
    </View>
  );
}
