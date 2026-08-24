import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { SellerRisk, MUTED } from './index';

export function SellerRiskRow({
  expanded,
  isLast,
  seller,
}: {
  expanded: boolean;
  isLast: boolean;
  seller: SellerRisk;
}) {
  const riskClass =
    seller.risk === 'High' ? 'bg-[#fde8e9] dark:bg-[#3a1f24]' : 'bg-[#fff4d6] dark:bg-[#3a301c]';
  const riskTextClass = seller.risk === 'High' ? 'text-[#de4b54]' : 'text-[#a77912]';

  return (
    <View
      className={`${isLast ? '' : 'border-b border-[#ececf0] dark:border-[#292a2f]'} pb-4 ${expanded ? '' : 'pt-4'}`}
    >
      <View className="flex-row items-center">
        <Ionicons
          color={MUTED}
          name={expanded ? 'chevron-up-outline' : 'chevron-down-outline'}
          size={15}
        />
        <View className="ml-3 flex-1">
          <Text className={cx(TYPE.rowLabel, 'text-[#3a3d44] dark:text-[#f4f4f5]')}>
            {seller.name}
          </Text>
          <Text className={cx(TYPE.rowMeta, 'mt-2 text-[#8d9098] dark:text-[#8f929b]')}>
            {seller.balance}
          </Text>
        </View>
        <View className="items-end">
          <View className={`rounded-full px-2 py-1 ${riskClass}`}>
            <Text className={`${TYPE.microStrong} ${riskTextClass}`}>● {seller.risk}</Text>
          </View>
          <Text className={cx(TYPE.rowMeta, 'mt-2 text-[#767a84] dark:text-[#a3a4ab]')}>
            {seller.share}
          </Text>
        </View>
      </View>
      {expanded && seller.rows ? (
        <View className="mt-4 gap-4 pl-7">
          {seller.rows.map((row) => (
            <View className="flex-row justify-between" key={row.label}>
              <Text className={cx(TYPE.rowMeta, 'text-[#8d9098] dark:text-[#8f929b]')}>
                {row.label}
              </Text>
              <Text className={cx(TYPE.rowLabel, 'text-[#424650] dark:text-[#e6e6e8]')}>
                {row.value}
              </Text>
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}
