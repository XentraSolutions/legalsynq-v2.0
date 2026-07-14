import { useEffect, useMemo, useState } from 'react';
import { Modal, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';

export type DateRangePickerValue = {
  endDate: string;
  startDate: string;
};

export interface DateRangePickerProps {
  value: DateRangePickerValue;
  isDark: boolean;
  onChange: (value: DateRangePickerValue) => void;
  containerClassName?: string;
  fieldLabel?: string;
  modalDescription?: string;
  modalTitle?: string;
}

type CalendarDay = {
  date: Date;
  isCurrentMonth: boolean;
};

const DEFAULT_DATE = new Date();
const WEEKDAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const MONTH_LABELS = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];
const RANGE_HIGHLIGHT = {
  dark: 'rgba(238, 113, 50, 0.22)',
  light: 'rgba(238, 113, 50, 0.15)',
} as const;

function padDatePart(value: number): string {
  return String(value).padStart(2, '0');
}

function formatPickerDate(date: Date): string {
  return `${padDatePart(date.getMonth() + 1)}/${padDatePart(date.getDate())}/${date.getFullYear()}`;
}

function parsePickerDate(value: string): Date {
  const [month, day, year] = value.split('/').map((part) => Number(part));
  if (!month || !day || !year) {
    return DEFAULT_DATE;
  }

  return new Date(year, month - 1, day);
}

function formatDateForDisplay(value: string): string {
  return value.split('/').join(' / ');
}

function formatDateRangeLabel(value: DateRangePickerValue): string {
  if (value.startDate === value.endDate) {
    return formatDateForDisplay(value.startDate);
  }

  return `${formatDateForDisplay(value.startDate)} - ${formatDateForDisplay(value.endDate)}`;
}

function formatReadableDate(date: Date): string {
  return `${date.getDate()} ${MONTH_LABELS[date.getMonth()].slice(0, 3)}, ${date.getFullYear()}`;
}

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function addMonths(date: Date, months: number): Date {
  return new Date(date.getFullYear(), date.getMonth() + months, 1);
}

function isSameCalendarDay(left: Date, right: Date): boolean {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}

function normalizeCalendarRange(start: Date, end: Date): { end: Date; start: Date } {
  return start <= end ? { start, end } : { start: end, end: start };
}

function isDateWithinRange(date: Date, start: Date, end: Date): boolean {
  const normalized = normalizeCalendarRange(start, end);
  const day = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  return (
    day >=
      new Date(
        normalized.start.getFullYear(),
        normalized.start.getMonth(),
        normalized.start.getDate()
      ).getTime() &&
    day <=
      new Date(
        normalized.end.getFullYear(),
        normalized.end.getMonth(),
        normalized.end.getDate()
      ).getTime()
  );
}

function buildCalendarDays(month: Date): CalendarDay[] {
  const firstDay = startOfMonth(month);
  const gridStart = new Date(firstDay);
  gridStart.setDate(firstDay.getDate() - firstDay.getDay());

  return Array.from({ length: 42 }, (_, index) => {
    const date = new Date(gridStart);
    date.setDate(gridStart.getDate() + index);
    return {
      date,
      isCurrentMonth: date.getMonth() === month.getMonth(),
    };
  });
}

export function DateRangePicker({
  value,
  isDark,
  onChange,
  containerClassName,
  fieldLabel,
  modalDescription = 'Filter results by selected start and end dates.',
  modalTitle = 'Date range',
}: DateRangePickerProps) {
  const [visible, setVisible] = useState(false);

  return (
    <View className={containerClassName}>
      <DateRangeField
        dateRange={value}
        fieldLabel={fieldLabel}
        isDark={isDark}
        onPress={() => setVisible(true)}
      />
      <DateRangePickerModal
        dateRange={value}
        isDark={isDark}
        modalDescription={modalDescription}
        modalTitle={modalTitle}
        visible={visible}
        onApply={onChange}
        onClose={() => setVisible(false)}
      />
    </View>
  );
}

function DateRangeField({
  dateRange,
  fieldLabel,
  focused = false,
  isDark,
  onPress,
}: {
  dateRange: DateRangePickerValue;
  fieldLabel?: string;
  focused?: boolean;
  isDark: boolean;
  onPress?: () => void;
}) {
  const fieldContent = (
    <>
      <Text
        className={cx(TYPE.dateLabel, 'flex-1 text-[#565a64] dark:text-[#e8e8eb]')}
        numberOfLines={1}
      >
        {formatDateRangeLabel(dateRange)}
      </Text>
      <Ionicons color={isDark ? '#a7a8b2' : '#6f737d'} name="calendar-clear-outline" size={16} />
    </>
  );
  const fieldClassName = cx(
    'h-9 flex-row items-center justify-between gap-3 rounded-xl bg-white px-3 dark:bg-[#202126]',
    focused ? 'border border-[#ee7132]' : 'border border-transparent'
  );
  const fieldStyle = {
    shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
    shadowOpacity: isDark ? 0.15 : 0.35,
    shadowRadius: 8,
    shadowOffset: { height: 3, width: 0 },
    elevation: 1,
  };

  return (
    <View>
      {fieldLabel ? (
        <Text className={cx(TYPE.formLabel, 'mb-2 text-[#202228] dark:text-white')}>
          {fieldLabel}
        </Text>
      ) : null}
      {onPress ? (
        <Pressable
          accessibilityRole="button"
          className={fieldClassName}
          style={fieldStyle}
          onPress={onPress}
        >
          {fieldContent}
        </Pressable>
      ) : (
        <View className={fieldClassName} style={fieldStyle}>
          {fieldContent}
        </View>
      )}
    </View>
  );
}

function DateRangePickerModal({
  dateRange,
  isDark,
  modalDescription,
  modalTitle,
  visible,
  onApply,
  onClose,
}: {
  dateRange: DateRangePickerValue;
  isDark: boolean;
  modalDescription: string;
  modalTitle: string;
  visible: boolean;
  onApply: (dateRange: DateRangePickerValue) => void;
  onClose: () => void;
}) {
  const [draftStart, setDraftStart] = useState(() => parsePickerDate(dateRange.startDate));
  const [draftEnd, setDraftEnd] = useState(() => parsePickerDate(dateRange.endDate));
  const [visibleMonth, setVisibleMonth] = useState(() =>
    startOfMonth(parsePickerDate(dateRange.endDate))
  );

  useEffect(() => {
    if (visible) {
      const start = parsePickerDate(dateRange.startDate);
      const end = parsePickerDate(dateRange.endDate);
      setDraftStart(start);
      setDraftEnd(end);
      setVisibleMonth(startOfMonth(end));
    }
  }, [dateRange.endDate, dateRange.startDate, visible]);

  const draftRange = useMemo<DateRangePickerValue>(
    () => ({
      startDate: formatPickerDate(draftStart),
      endDate: formatPickerDate(draftEnd),
    }),
    [draftEnd, draftStart]
  );

  const selectedRange = useMemo(
    () => normalizeCalendarRange(draftStart, draftEnd),
    [draftEnd, draftStart]
  );

  const calendarRows = useMemo(() => {
    const days = buildCalendarDays(visibleMonth);
    return Array.from({ length: 6 }, (_, rowIndex) => days.slice(rowIndex * 7, rowIndex * 7 + 7));
  }, [visibleMonth]);

  const handleSelectDate = (selectedDate: Date) => {
    const normalizedDate = new Date(
      selectedDate.getFullYear(),
      selectedDate.getMonth(),
      selectedDate.getDate()
    );

    if (!isSameCalendarDay(draftStart, draftEnd)) {
      setDraftStart(normalizedDate);
      setDraftEnd(normalizedDate);
      return;
    }

    if (normalizedDate < draftStart) {
      setDraftStart(normalizedDate);
      setDraftEnd(draftStart);
      return;
    }

    setDraftEnd(normalizedDate);
  };

  const handleApply = () => {
    const { end, start } = normalizeCalendarRange(draftStart, draftEnd);
    onApply({
      startDate: formatPickerDate(start),
      endDate: formatPickerDate(end),
    });
    onClose();
  };

  return (
    <Modal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 justify-end bg-black/35 px-4 pb-6 dark:bg-black/70">
        <View
          className="rounded-[24px] bg-white p-4 dark:bg-[#191a1f]"
          style={{
            shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
            shadowOpacity: isDark ? 0.28 : 0.45,
            shadowRadius: 12,
            shadowOffset: { height: 6, width: 0 },
            elevation: 4,
          }}
        >
          <View className="mb-4 flex-row items-start justify-between">
            <View className="flex-1 pr-4">
              <Text className={cx(TYPE.cardTitle, 'text-[#202228] dark:text-white')}>
                {modalTitle}
              </Text>
              <Text className={cx(TYPE.cardDescription, 'mt-1 text-[#8a8d96] dark:text-[#9ca0aa]')}>
                {modalDescription}
              </Text>
            </View>
            <Pressable accessibilityRole="button" hitSlop={12} onPress={onClose}>
              <Ionicons color={isDark ? '#a1a1aa' : '#6f737d'} name="close-outline" size={22} />
            </Pressable>
          </View>

          <DateRangeField dateRange={draftRange} fieldLabel="Date" focused isDark={isDark} />

          <View className="mt-4 rounded-[24px] bg-white px-2 pb-3 pt-2 dark:bg-[#202126]">
            <View className="mb-3 flex-row items-center justify-between px-2">
              <View className="flex-row items-center gap-1">
                <Text className={cx(TYPE.cardTitle, 'text-[#202228] dark:text-white')}>
                  {MONTH_LABELS[visibleMonth.getMonth()]}
                </Text>
                <Ionicons
                  color={isDark ? '#a7a8b2' : '#6f737d'}
                  name="chevron-down-outline"
                  size={16}
                />
                <Text className={cx(TYPE.cardTitle, 'text-[#202228] dark:text-white')}>
                  {visibleMonth.getFullYear()}
                </Text>
              </View>
              <View className="flex-row items-center gap-2">
                <CalendarNavButton
                  direction="back"
                  isDark={isDark}
                  onPress={() => setVisibleMonth((month) => addMonths(month, -1))}
                />
                <CalendarNavButton
                  direction="forward"
                  isDark={isDark}
                  onPress={() => setVisibleMonth((month) => addMonths(month, 1))}
                />
              </View>
            </View>

            <View className="mb-1 flex-row">
              {WEEKDAY_LABELS.map((weekday) => (
                <Text
                  key={weekday}
                  className={cx(TYPE.formLabel, 'flex-1 text-center text-[#8a8d96]')}
                >
                  {weekday}
                </Text>
              ))}
            </View>

            {calendarRows.map((row, rowIndex) => (
              <View key={`week-${rowIndex}`} className="flex-row justify-between py-0.5">
                {row.map((day) => (
                  <CalendarDayButton
                    key={day.date.getTime()}
                    day={day}
                    isDark={isDark}
                    isInRange={isDateWithinRange(day.date, selectedRange.start, selectedRange.end)}
                    isSelectedEnd={isSameCalendarDay(day.date, selectedRange.end)}
                    isSelectedStart={isSameCalendarDay(day.date, selectedRange.start)}
                    onPress={() => handleSelectDate(day.date)}
                  />
                ))}
              </View>
            ))}

            <Text
              className={cx(
                TYPE.cardDescription,
                'mt-3 px-2 text-center text-[#6f737d] dark:text-[#a7a8b2]'
              )}
            >
              Selected: {formatReadableDate(selectedRange.start)} -{' '}
              {formatReadableDate(selectedRange.end)}
            </Text>
          </View>

          <View className="mt-5 flex-row gap-3">
            <Pressable
              accessibilityRole="button"
              className="h-10 flex-1 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]"
              onPress={onClose}
            >
              <Text className={cx(TYPE.cta, 'text-[#555964] dark:text-[#e7e7e9]')}>Cancel</Text>
            </Pressable>
            <Pressable
              accessibilityRole="button"
              className="h-10 flex-1 items-center justify-center rounded-full bg-[#f97332]"
              onPress={handleApply}
            >
              <Text className={cx(TYPE.cta, 'text-[#15161a]')}>Apply</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

function CalendarNavButton({
  direction,
  isDark,
  onPress,
}: {
  direction: 'back' | 'forward';
  isDark: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className="h-6 w-6 items-center justify-center rounded-full bg-[#f5f5f6] dark:bg-[#2a2b30]"
      onPress={onPress}
    >
      <Ionicons
        color={isDark ? '#d7d8df' : '#565a64'}
        name={direction === 'back' ? 'chevron-back-outline' : 'chevron-forward-outline'}
        size={15}
      />
    </Pressable>
  );
}

function CalendarDayButton({
  day,
  isDark,
  isInRange,
  isSelectedEnd,
  isSelectedStart,
  onPress,
}: {
  day: CalendarDay;
  isDark: boolean;
  isInRange: boolean;
  isSelectedEnd: boolean;
  isSelectedStart: boolean;
  onPress: () => void;
}) {
  const isSelected = isSelectedStart || isSelectedEnd;
  const mutedText = day.isCurrentMonth
    ? 'text-[#30333a] dark:text-[#e8e8eb]'
    : 'text-[#b9bbc2] dark:text-[#666a74]';

  return (
    <Pressable accessibilityRole="button" className="h-9 flex-1 items-center" onPress={onPress}>
      <View
        className={cx(
          'h-9 w-9 items-center justify-center',
          isSelected ? 'rounded-full bg-[#ee7132]' : 'rounded-lg'
        )}
        style={
          isInRange && !isSelected
            ? { backgroundColor: isDark ? RANGE_HIGHLIGHT.dark : RANGE_HIGHLIGHT.light }
            : undefined
        }
      >
        <Text className={cx(TYPE.dateLabel, isSelected ? 'text-white' : mutedText)}>
          {day.date.getDate()}
        </Text>
      </View>
    </Pressable>
  );
}
